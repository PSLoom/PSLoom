// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using PSLoom.Runtime.Loom;
using PSLoom.Warp.Dsl;

namespace PSLoom.Runtime.Sheds;

/// <summary>
///   Applies one staged statement after its draft: a verb with the draft vocabulary in a run of its own, any other statement in the
///   global scope. Never throws; the outcome is recorded on the entry.
/// </summary>
internal static class ShedApplier {
  public static bool Apply(LoomSession session, ShedEntry entry) {
    ArgumentNullException.ThrowIfNull(session);
    ArgumentNullException.ThrowIfNull(entry);

    var started = Stopwatch.GetTimestamp();
    var run = new LoomRun(true);

    try {
      if (!ShedConditions.Hold(session, entry, run)) {
        return false;
      }

      session.PushRun(run);
      run.PushFrame(new DraftFrame(), typeof(DraftScope));

      try {
        var script = ShedRewriter.StatementScript(entry.Declaration, entry.File);
        var output = entry.Declaration.IsVerb
          ? script.InvokeWithContext(session.Verbs.TableFor(typeof(DraftScope)), [])
          : session.Engine!.InvokeCommand.InvokeScript(false, script, null);

        if (!entry.Declaration.Lucid &&
            output.Count > 0) {
          session.Engine!.InvokeCommand.InvokeScript("param($Output) $Output | Out-Host", output);
        }
      }
      catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException or PipelineStoppedException)) {
        run.Report(LoomException.FromStatement(exception));
      }
      finally {
        run.PopFrame();
        run.Active.Clear();
        session.PopRun(run);
      }

      var elapsed = Stopwatch.GetElapsedTime(started);
      entry.AppliedItems = [.. run.Ledger];

      if (run.Errors.Count > 0) {
        entry.Complete(ShedState.Failed, elapsed, [.. run.Errors]);
      }
      else {
        entry.Complete(ShedState.Applied, elapsed, []);
        ShedConditions.RunAtLoad(session, entry, run);
      }

      session.Sheds.Timings.Add(new LoomTiming(LoomPhase.Deferred, entry.Statement, null, entry.Line, 0, elapsed, elapsed));
      return entry.State == ShedState.Applied;
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      entry.Complete(ShedState.Failed, Stopwatch.GetElapsedTime(started),
        [LoomException.ShedApplyFailed(entry.Line, entry.Statement, exception).ToErrorRecord()]);
      return false;
    }
  }
}
