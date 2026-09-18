// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Treadles;

namespace PSLoom.Runtime.Treadles;

/// <summary>
///   What one write to the catalog did: the definition afterwards (<see langword="null" /> on a removal), the one it replaced, and
///   the failures of the subscribers it notified, which the caller reports.
/// </summary>
internal sealed class TreadleWriteOutcome(TreadleDefinition? current, TreadleDefinition? previous, IReadOnlyList<Exception> subscriberFailures) {
  public TreadleDefinition? Current { get; } = current;

  public TreadleDefinition? Previous { get; } = previous;

  public IReadOnlyList<Exception> SubscriberFailures { get; } = subscriberFailures;

  /// <summary>
  ///   Gets the name the write touched.
  /// </summary>
  public string Name => (Current ?? Previous)!.Name;

  /// <summary>
  ///   Gets a value indicating whether the write replaced an existing treadle.
  /// </summary>
  public bool Replaced => Current is not null && Previous is not null;
}
