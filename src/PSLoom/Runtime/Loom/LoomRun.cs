// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;

namespace PSLoom.Runtime.Loom;

/// <summary>
///   One execution of DSL code (a draft, or a harness body run through <c>IDslRunner</c>): its frame stack, the verbs
///   currently running, collected errors, timings and, for drafts, the reweave ledger. Used only on the runspace's thread.
/// </summary>
internal sealed class LoomRun(bool isDraft, IReadOnlyList<LedgerItem>? previousLedger = null) {
  private readonly List<ErrorRecord> _errors = [];
  private readonly Stack<(object Frame, Type Scope)> _frames = new();
  private readonly Dictionary<string, int> _keyOccurrences = new(StringComparer.Ordinal);
  private readonly List<LedgerItem> _ledger = [];
  private readonly Dictionary<string, LedgerItem>? _previous = previousLedger?.ToDictionary(item => item.Entry.Key, StringComparer.Ordinal);
  private readonly IReadOnlyList<LedgerItem> _previousInOrder = previousLedger ?? [];
  private readonly List<LoomTiming> _timings = [];

  public bool IsDraft { get; } = isDraft;

  /// <summary>
  ///   Gets a value indicating whether this run re-weaves a previous one.
  /// </summary>
  public bool IsReweave => _previous is not null;

  public long StartedAt { get; } = Stopwatch.GetTimestamp();

  public IReadOnlyList<ErrorRecord> Errors => _errors;

  public IReadOnlyList<LoomTiming> Timings => _timings;

  /// <summary>
  ///   Gets the error-free top-level verb invocations of this run, in order.
  /// </summary>
  public IReadOnlyList<LedgerItem> Ledger => _ledger;

  /// <summary>
  ///   Gets the verbs currently executing, innermost on top.
  /// </summary>
  public Stack<VerbInvocation> Active { get; } = new();

  /// <summary>
  ///   Gets the scope of the innermost frame.
  /// </summary>
  public Type CurrentScope => _frames.Peek().Scope;

  public int FrameDepth => _frames.Count;

  public void Report(ErrorRecord errorRecord)
    => _errors.Add(errorRecord);

  public void AddTiming(LoomTiming timing)
    => _timings.Add(timing);

  public void PushFrame(object frame, Type scope)
    => _frames.Push((frame, scope));

  public void PopFrame()
    => _frames.Pop();

  public TFrame? FindFrame<TFrame>() where TFrame : class {
    foreach (var (frame, _) in _frames) {
      if (frame is TFrame match) {
        return match;
      }
    }

    return null;
  }

  /// <summary>
  ///   Makes a key unique within the run: the second invocation with the same key gets <c>#2</c>, and so on.
  /// </summary>
  public string DisambiguateKey(string key) {
    var occurrence = _keyOccurrences.GetValueOrDefault(key) + 1;
    _keyOccurrences[key] = occurrence;
    return occurrence == 1 ? key : $"{key}#{occurrence}";
  }

  /// <summary>
  ///   Checks whether the previous run applied exactly this invocation.
  /// </summary>
  public bool IsUnchanged(string key, UInt128 fingerprint)
    => _previous is not null && _previous.TryGetValue(key, out var item) && item.Entry.Fingerprint == fingerprint;

  public void Record(LedgerItem item)
    => _ledger.Add(item);

  /// <summary>
  ///   Gets the previous invocations absent from this run, most recent first.
  /// </summary>
  public IEnumerable<LedgerItem> Removed() {
    if (_previous is null) {
      return [];
    }

    var current = _ledger.Select(item => item.Entry.Key).ToHashSet(StringComparer.Ordinal);
    return _previousInOrder.Where(item => !current.Contains(item.Entry.Key)).Reverse();
  }
}
