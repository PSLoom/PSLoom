// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using PSLoom.Runtime.Verbs;
using PSLoom.Warp.Dsl;
using PSLoom.Warp.Kernel;

namespace PSLoom.Runtime.Loom;

/// <summary>
///   One running verb: routes its errors into the run, times it inclusive and exclusive of nested verbs, and records top-level
///   draft invocations in the run's reweave ledger when they complete without error.
/// </summary>
internal sealed class VerbInvocation : IVerbInvocation {
  private readonly int _errorsAtStart;
  private readonly ReweaveEntry? _ledgerEntry;
  private readonly LoomRun _run;
  private readonly LoomSession _session;
  private readonly long _startedAt;
  private TimeSpan _childTime;
  private bool _completed;
  private LoomContext? _context;

  public VerbInvocation(LoomSession session, LoomRun run, VerbDescriptor descriptor, bool shouldWeave, int? line, ReweaveEntry? ledgerEntry = null) {
    _session = session;
    _run = run;
    _ledgerEntry = ledgerEntry;
    _errorsAtStart = run.Errors.Count;
    Descriptor = descriptor;
    ShouldWeave = shouldWeave;
    Line = line;
    Depth = run.Active.Count;
    _startedAt = Stopwatch.GetTimestamp();
    run.Active.Push(this);
  }

  public VerbDescriptor Descriptor { get; }

  public int? Line { get; }

  public int Depth { get; }

  /// <inheritdoc />
  public ILoomContext Loom => _context ??= new LoomContext(_session, _run);

  /// <inheritdoc />
  public bool ShouldWeave { get; }

  /// <inheritdoc />
  public void ReportError(ErrorRecord errorRecord)
    => _run.Report(errorRecord);

  /// <inheritdoc />
  public void Complete() {
    if (_completed) {
      return;
    }

    _completed = true;
    var inclusive = Stopwatch.GetElapsedTime(_startedAt);

    if (_run.Active.TryPeek(out var top) &&
        ReferenceEquals(top, this)) {
      _run.Active.Pop();
    }

    if (_run.Active.TryPeek(out var parent)) {
      parent._childTime += inclusive;
    }

    if (_ledgerEntry is not null &&
        _run.Errors.Count == _errorsAtStart) {
      _run.Record(new LedgerItem(_ledgerEntry, Descriptor));
    }

    _run.AddTiming(new LoomTiming(LoomPhase.Verb, Descriptor.Name, Descriptor.Owner, Line, Depth, inclusive, inclusive - _childTime));
  }
}
