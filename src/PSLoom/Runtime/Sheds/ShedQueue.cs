// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;

namespace PSLoom.Runtime.Sheds;

/// <summary>
///   Staged statements waiting to apply, ordered by slot and then by draft order. It never skips an entry to reach a later one:
///   skipping would silently reorder loads the draft ordered on purpose.
/// </summary>
internal sealed class ShedQueue {
  private readonly List<ShedEntry> _entries = [];

  public int Count => _entries.Count;

  public IReadOnlyList<ShedEntry> Entries => _entries;

  /// <summary>
  ///   Ranks a slot: <c>0a</c> &lt; <c>0b</c> &lt; <c>0c</c> &lt; <c>1a</c>.
  /// </summary>
  public static long SlotRank(string slot)
    => (long.Parse(slot[..^1], System.Globalization.CultureInfo.InvariantCulture) * 3) + (char.ToLowerInvariant(slot[^1]) - 'a');

  public void Enqueue(ShedEntry entry) {
    ArgumentNullException.ThrowIfNull(entry);

    var rank = SlotRank(entry.Slot);
    var position = _entries.FindIndex(existing =>
      SlotRank(existing.Slot) > rank || (SlotRank(existing.Slot) == rank && existing.Line > entry.Line));

    _entries.Insert(position < 0 ? _entries.Count : position, entry);
  }

  public bool Remove(ShedEntry entry)
    => _entries.Remove(entry);

  /// <summary>
  ///   Applies entries until <paramref name="slice" /> has elapsed, and always at least one.
  /// </summary>
  /// <returns>How many entries were taken from the queue.</returns>
  public int DrainSlice(Func<ShedEntry, bool> apply, Func<long> timestamp, TimeSpan slice) {
    ArgumentNullException.ThrowIfNull(apply);
    ArgumentNullException.ThrowIfNull(timestamp);

    var started = timestamp();
    var taken = 0;

    while (_entries.Count > 0) {
      var entry = _entries[0];
      _entries.RemoveAt(0);
      apply(entry);
      taken++;

      if (Stopwatch.GetElapsedTime(started, timestamp()) >= slice) {
        break;
      }
    }

    return taken;
  }

  /// <summary>
  ///   Applies every entry, in order.
  /// </summary>
  public int DrainAll(Func<ShedEntry, bool> apply) {
    ArgumentNullException.ThrowIfNull(apply);

    var taken = 0;

    while (_entries.Count > 0) {
      var entry = _entries[0];
      _entries.RemoveAt(0);
      apply(entry);
      taken++;
    }

    return taken;
  }
}
