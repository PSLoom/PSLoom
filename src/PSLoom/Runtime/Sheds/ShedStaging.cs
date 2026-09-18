// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using PSLoom.Runtime.Hooks;
using PSLoom.Runtime.Loom;
using PSLoom.Warp.Dsl;
using PSLoom.Warp.Hooks;

namespace PSLoom.Runtime.Sheds;

/// <summary>
///   The staged statements of one session: what the last draft staged, the queue of those still waiting, and the timings of what
///   applied after the draft.
/// </summary>
internal sealed class ShedStaging(LoomSession session) {
  private List<ShedEntry> _entries = [];
  private List<ShedEntry> _removed = [];
  private bool _wired;

  /// <summary>Gets the entries waiting for a prompt, an idle tick or a rescue.</summary>
  public ShedQueue Queue { get; } = new();

  /// <summary>Gets or sets how the session decides it is interactive; tests replace it.</summary>
  internal Func<bool> IsInteractive { get; set; } = () => InteractiveHost.Detect(session.Engine);

  /// <summary>Gets or sets the clock a firing measures its slice with; tests replace it.</summary>
  internal Func<long> Timestamp { get; set; } = Stopwatch.GetTimestamp;

  /// <summary>Gets the time a firing may spend; it always applies at least one entry.</summary>
  internal TimeSpan Slice { get; } = TimeSpan.FromMilliseconds(15);

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

    var previous = run.IsReweave
      ? _entries.ToDictionary(entry => entry.Key, StringComparer.Ordinal)
      : new Dictionary<string, ShedEntry>(StringComparer.Ordinal);
    _removed = [];

    for (var index = 0; index < entries.Count; index++) {
      if (!previous.Remove(entries[index].Key, out var old)) {
        continue;
      }

      var unchanged = old.Fingerprint == entries[index].Fingerprint;

      if (unchanged &&
          old.State is ShedState.Pending or ShedState.Applied) {
        old.Adopted = true;
        entries[index] = old; // the rewritten draft's index now points at the kept entry
        continue;
      }

      if (old.State == ShedState.Pending) {
        Queue.Remove(old);
      }
    }

    foreach (var old in previous.Values) {
      if (old.State == ShedState.Pending) {
        Queue.Remove(old);
      }
      else if (old is { State: ShedState.Applied, Timing: not ShedTiming.Now }) {
        _removed.Add(old);
      }
    }

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

    // Deferred statements a reweave removed applied in runs of their own, so the draft's ledger never saw them: revert them here,
    // unless the new draft applied the same invocation itself.
    if (_removed.Count > 0 &&
        session.CurrentRun is null) {
      var reapplied = session.Ledger.Select(item => item.Entry.Key).ToHashSet(StringComparer.Ordinal);
      var run = new LoomRun(false);

      LedgerReverter.Revert(_removed.SelectMany(entry => entry.AppliedItems).Where(item => !reapplied.Contains(item.Entry.Key)).Reverse(), run,
        cmdlet);

      foreach (var error in run.Errors) {
        cmdlet.WriteError(error);
      }

      _removed = [];
    }

    // Apply-now statements that failed were already written as errors of Invoke-Loom; the prompt warning must not repeat them.
    foreach (var entry in _entries.Where(entry => entry.State == ShedState.Failed)) {
      entry.Warned = true;
    }

    if (Queue.Count == 0) {
      return;
    }

    if (IsInteractive()) {
      EnsureWired();
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

  internal void OnPrompt() {
    try {
      if (Queue.Count > 0) {
        Queue.DrainSlice(entry => ShedApplier.Apply(session, entry), Timestamp, Slice);
      }

      WarnAboutFailures();
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      // A prompt draw must never break; the applier already records per-entry failures.
    }
  }

  internal void OnIdle() {
    try {
      if (Queue.Count > 0) {
        Queue.DrainSlice(entry => ShedApplier.Apply(session, entry), Timestamp, Slice);
      }
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      // Idle ticks never break.
    }
  }

  internal void OnCommandNotFound(CommandNotFoundInvocation invocation) {
    try {
      if (Queue.Count == 0) {
        return;
      }

      // The kernel cannot know which entry provides which command, so everything left applies before looking again.
      Queue.DrainAll(entry => ShedApplier.Apply(session, entry));

      if (session.Engine?.InvokeCommand.GetCommand(invocation.CommandName, CommandTypes.All) is { } command) {
        invocation.EventArgs.Command = command;
        invocation.EventArgs.StopSearch = true;
      }
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      // Command lookup never breaks because of staging.
    }
  }

  private void EnsureWired() {
    if (_wired) {
      return;
    }

    var bus = HookBus.PerRunspace.For(session.Runspace);
    bus.AddInternal(HookKind.PrePrompt, _ => {
      OnPrompt();
      return null;
    });
    bus.AddInternal(HookKind.Idle, _ => {
      OnIdle();
      return null;
    });
    bus.AddInternal(HookKind.CommandNotFound, invocation => {
      OnCommandNotFound((CommandNotFoundInvocation)invocation);
      return null;
    });

    bus.Wiring.EnsureWired(HookKind.PrePrompt);
    bus.Wiring.EnsureWired(HookKind.Idle);
    bus.Wiring.EnsureWired(HookKind.CommandNotFound);
    _wired = true;
  }

  private void WarnAboutFailures() {
    var failed = _entries.Where(entry => entry is { State: ShedState.Failed, Warned: false }).ToArray();

    foreach (var entry in failed) {
      entry.Warned = true;
    }

    var reported = failed.Where(entry => !entry.Declaration.Silent).ToArray();

    if (reported.Length == 0 ||
        session.Engine is not { } engine) {
      return;
    }

    var noun = reported.Length == 1 ? "staged statement" : "staged statements";
    var list = string.Join("; ", reported.Select(entry => $"line {entry.Line}: {entry.Statement}"));

    // Write-Warning reaches the prompt pipeline's warning stream: the console shows it, and hosted runspaces capture it.
    engine.InvokeCommand.InvokeScript("param($Message) Write-Warning $Message",
      $"PSLoom: {reported.Length} {noun} failed ({list}). Run Get-Shed -State Failed.");
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
