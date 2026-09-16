// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using PSLoom.Runtime.Loom;
using PSLoom.Warp.Dsl;

namespace PSLoom.Runtime.Sheds;

/// <summary>
///   The staged statements of one session: what the last draft staged, the queue of those still waiting, and the timings of what
///   applied after the draft.
/// </summary>
internal sealed class ShedStaging(LoomSession session) {
  private List<ShedEntry> _entries = [];

  /// <summary>Gets the entries waiting for a prompt, an idle tick or a rescue.</summary>
  public ShedQueue Queue { get; } = new();

  /// <summary>Gets or sets how the session decides it is interactive; tests replace it.</summary>
  internal Func<bool> IsInteractive { get; set; } = () => InteractiveHost.Detect(session.Engine);

  /// <summary>Gets every statement the last woven draft staged, in draft order.</summary>
  public IReadOnlyList<ShedEntry> Entries => _entries;

  /// <summary>Gets the timings of statements applied after the draft.</summary>
  public List<LoomTiming> Timings { get; } = [];

  /// <summary>
  ///   Prepares a run: creates its entries and returns the draft to execute (rewritten only when something is staged).
  /// </summary>
  public ScriptBlock Begin(LoomRun run, ScriptBlock draft, IReadOnlyList<ShedDeclaration> declarations) {
    ArgumentNullException.ThrowIfNull(run);
    ArgumentNullException.ThrowIfNull(draft);
    ArgumentNullException.ThrowIfNull(declarations);

    var started = Stopwatch.GetTimestamp();
    var entries = declarations.Select(declaration => CreateEntry(run, declaration, draft.File)).ToList();

    run.Sheds = entries;
    _entries = entries;

    var rewritten = ShedRewriter.Rewrite(draft, declarations);

    if (declarations.Count > 0) {
      var elapsed = Stopwatch.GetElapsedTime(started);
      run.AddTiming(new LoomTiming(LoomPhase.Capture, nameof(LoomPhase.Capture), null, null, 0, elapsed, elapsed, declarations.Count));
    }

    return rewritten;
  }

  /// <summary>
  ///   Queues a statement staged for later.
  /// </summary>
  public void Capture(ShedEntry entry) {
    entry.State = ShedState.Pending;
    Queue.Enqueue(entry);
  }

  /// <summary>
  ///   Finishes a draft run: in a host that will never draw a prompt, applies the queue now and writes what failed on the cmdlet.
  /// </summary>
  public void Complete(PSCmdlet cmdlet) {
    ArgumentNullException.ThrowIfNull(cmdlet);

    // Apply-now statements that failed were already written as errors of Invoke-Loom; the prompt warning must not repeat them.
    foreach (var entry in _entries.Where(entry => entry.State == ShedState.Failed)) {
      entry.Warned = true;
    }

    if (Queue.Count == 0) {
      return;
    }

    if (IsInteractive()) {
      return;
    }

    Queue.DrainAll(entry => ShedApplier.Apply(session, entry));

    foreach (var entry in _entries.Where(entry => entry is { State: ShedState.Failed, Warned: false })) {
      entry.Warned = true;

      foreach (var error in entry.Errors) {
        cmdlet.WriteError(error);
      }
    }
  }

  private static ShedEntry CreateEntry(LoomRun run, ShedDeclaration declaration, string? file) {
    var values = new Dictionary<string, object?> {
      ["Statement"] = Normalize(declaration.Statement.Extent.Text),
      ["Timing"] = declaration.Timing.ToString(),
      ["Slot"] = declaration.Slot,
      ["LoadIf"] = declaration.LoadIf,
      ["RequiresCommand"] = declaration.RequiresCommand,
      ["Lucid"] = declaration.Lucid,
      ["Silent"] = declaration.Silent,
      ["AtLoad"] = declaration.AtLoad
    };

    var fingerprint = ReweaveFingerprint.Compute(values);
    var key = run.DisambiguateKey("Shed" + Normalize(declaration.Statement.Extent.Text));

    return new ShedEntry(declaration, file, key, fingerprint);
  }

  private static string Normalize(string text)
    => string.Join(' ', text.Split((char[])[' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
}
