// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;

namespace PSLoom.Runtime.Loom;

/// <summary>
///   One execution of DSL code (a draft, or a harness body run through <c>IDslRunner</c>): its frame stack, the verbs
///   currently running, collected errors and timings. Used only on the runspace's thread.
/// </summary>
internal sealed class LoomRun(bool isDraft) {
  private readonly List<ErrorRecord> _errors = [];
  private readonly Stack<(object Frame, Type Scope)> _frames = new();
  private readonly List<LoomTiming> _timings = [];

  public bool IsDraft { get; } = isDraft;

  public long StartedAt { get; } = Stopwatch.GetTimestamp();

  public IReadOnlyList<ErrorRecord> Errors => _errors;

  public IReadOnlyList<LoomTiming> Timings => _timings;

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
}
