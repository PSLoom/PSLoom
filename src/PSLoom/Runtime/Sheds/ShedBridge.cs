// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using PSLoom.Runtime.Loom;
using PSLoom.Warp.Dsl;

namespace PSLoom.Runtime.Sheds;

/// <summary>
///   What a rewritten draft calls in place of a staged statement. Public because the rewritten script names this type; it is not part
///   of PSLoom's supported surface. Each method reports failures on the draft's error channel instead of throwing.
/// </summary>
public static class ShedBridge {
  /// <summary>
  ///   Decides whether an apply-now statement runs: evaluates its conditions and starts measuring it.
  /// </summary>
  public static bool Admit(int index) {
    var (session, run, entry) = Resolve(index);

    if (entry.Adopted) {
      // Unchanged and already applied: keep its verbs in the new ledger, or the next reweave would revert them as removed.
      foreach (var item in entry.AppliedItems) {
        run.Record(item);
      }

      return false;
    }

    if (!ShedConditions.Hold(session, entry, run)) {
      return false;
    }

    entry.ErrorsAtAdmit = run.Errors.Count;
    entry.LedgerAtAdmit = run.Ledger.Count;
    entry.AdmittedAt = Stopwatch.GetTimestamp();
    return true;
  }

  /// <summary>
  ///   Completes an apply-now statement: marks it applied or failed and runs <c>-AtLoad</c>.
  /// </summary>
  public static void Applied(int index) {
    var (session, run, entry) = Resolve(index);
    var elapsed = Stopwatch.GetElapsedTime(entry.AdmittedAt);
    var errors = run.Errors.Skip(entry.ErrorsAtAdmit).ToArray();

    entry.AppliedItems = [.. run.Ledger.Skip(entry.LedgerAtAdmit)];

    if (errors.Length > 0) {
      entry.Complete(ShedState.Failed, elapsed, errors);
      return;
    }

    entry.Complete(ShedState.Applied, elapsed, []);
    ShedConditions.RunAtLoad(session, entry, run);
  }

  /// <summary>
  ///   Records an apply-now statement that failed before completing: the error goes to the draft's channel once, and the entry is
  ///   marked failed.
  /// </summary>
  public static void Failed(int index, ErrorRecord error) {
    var (_, run, entry) = Resolve(index);

    run.Report(error);
    entry.Complete(ShedState.Failed, Stopwatch.GetElapsedTime(entry.AdmittedAt), [.. run.Errors.Skip(entry.ErrorsAtAdmit)]);
  }

  /// <summary>
  ///   Captures a statement staged for later.
  /// </summary>
  public static void Capture(int index) {
    var (session, _, entry) = Resolve(index);

    if (entry.Adopted) {
      return; // still queued, or already applied: an unchanged staged statement is not captured twice
    }

    session.Sheds.Capture(entry);
  }

  private static (LoomSession Session, LoomRun Run, ShedEntry Entry) Resolve(int index) {
    var session = LoomSession.PerRunspace.ForCurrent();
    var run = session.CurrentRun ?? throw new InvalidOperationException("A staged statement ran outside its draft.");

    return (session, run, run.Sheds[index]);
  }
}
