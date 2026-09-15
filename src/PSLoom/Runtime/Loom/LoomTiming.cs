// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Runtime.Loom;

/// <summary>
///   One timing row reported by <c>Measure-Loom</c>.
/// </summary>
public sealed class LoomTiming {
  internal LoomTiming(LoomPhase phase, string name, string? harness, int? line, int depth, TimeSpan inclusive, TimeSpan exclusive, int count = 1) {
    Phase = phase;
    Name = name;
    Harness = harness;
    Line = line;
    Depth = depth;
    Inclusive = inclusive;
    Exclusive = exclusive;
    Count = count;
  }

  /// <summary>
  ///   Gets the phase.
  /// </summary>
  public LoomPhase Phase { get; }

  /// <summary>
  ///   Gets the verb, harness or phase name.
  /// </summary>
  public string Name { get; }

  /// <summary>
  ///   Gets the harness that owns the verb or was imported (<c>PSLoom</c> for kernel verbs).
  /// </summary>
  public string? Harness { get; }

  /// <summary>
  ///   Gets the draft line of a verb invocation.
  /// </summary>
  public int? Line { get; }

  /// <summary>
  ///   Gets the nesting depth of a verb invocation (0 for top-level statements).
  /// </summary>
  public int Depth { get; }

  /// <summary>
  ///   Gets the time including nested verbs.
  /// </summary>
  public TimeSpan Inclusive { get; }

  /// <summary>
  ///   Gets the time excluding nested verbs.
  /// </summary>
  public TimeSpan Exclusive { get; }

  /// <summary>
  ///   Gets how many invocations a grouped row aggregates.
  /// </summary>
  public int Count { get; }
}
