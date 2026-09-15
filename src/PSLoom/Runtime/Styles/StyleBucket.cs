// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Collections.Concurrent;
using PSLoom.Warp.Styles;

namespace PSLoom.Runtime.Styles;

/// <summary>
///   Every definition of one style name. Reads are lock-free against an immutable state; writes (serialized by the store)
///   replace the whole state, which also discards this name's resolution cache and nothing else.
/// </summary>
internal sealed class StyleBucket {
  internal const int MAX_CACHED_CONTEXTS = 1024;

  private volatile State _state = State.Empty;

  /// <summary>
  ///   Gets every definition, in no particular order.
  /// </summary>
  public IReadOnlyCollection<StyleDefinition> Definitions {
    get {
      var entries = _state.Sorted;
      var result = new StyleDefinition[entries.Length];

      for (var index = 0; index < entries.Length; index++) {
        result[index] = entries[index].Definition;
      }

      return result;
    }
  }

  /// <summary>
  ///   Gets a value indicating whether the bucket has no definition.
  /// </summary>
  public bool IsEmpty => _state.Sorted.Length == 0;

  /// <summary>
  ///   Resolves the winning definition for a concrete context: the longest literal prefix, then the highest sequence.
  /// </summary>
  public StyleDefinition? Resolve(string context) {
    var state = _state;

    if (state.Cache.TryGetValue(context, out var cached)) {
      return cached;
    }

    StyleDefinition? winner = null;

    foreach (var entry in state.Sorted) {
      if (!entry.Matcher.IsMatch(context)) {
        continue;
      }

      winner = entry.Definition;
      break;
    }

    if (state.CachedCount < MAX_CACHED_CONTEXTS &&
        state.Cache.TryAdd(context, winner)) {
      Interlocked.Increment(ref state.CachedCount);
    }

    return winner;
  }

  /// <summary>
  ///   Finds the definition stored under an exact context pattern.
  /// </summary>
  public StyleDefinition? Find(string context)
    => _state.ByContext.TryGetValue(context, out var entry) ? entry.Definition : null;

  /// <summary>
  ///   Adds or replaces the definition for its exact context pattern. Caller holds the store's write lock.
  /// </summary>
  public void Set(StyleEntry entry) {
    var current = _state;
    var byContext = new Dictionary<string, StyleEntry>(current.ByContext, StringComparer.Ordinal) {
      [entry.Definition.Context] = entry
    };

    _state = new State(Sort(byContext.Values), byContext);
  }

  /// <summary>
  ///   Removes the definition for an exact context pattern. Caller holds the store's write lock.
  /// </summary>
  public StyleDefinition? Remove(string context) {
    var current = _state;

    if (!current.ByContext.TryGetValue(context, out var removed)) {
      return null;
    }

    var byContext = new Dictionary<string, StyleEntry>(current.ByContext, StringComparer.Ordinal);
    byContext.Remove(context);

    _state = new State(Sort(byContext.Values), byContext);
    return removed.Definition;
  }

  private static StyleEntry[] Sort(IEnumerable<StyleEntry> entries) {
    var sorted = entries.ToArray();

    Array.Sort(sorted, static (left, right) => {
      var bySpecificity = right.Matcher.LiteralPrefixLength.CompareTo(left.Matcher.LiteralPrefixLength);
      return bySpecificity != 0 ? bySpecificity : right.Definition.Sequence.CompareTo(left.Definition.Sequence);
    });

    return sorted;
  }

  private sealed class State(StyleEntry[] sorted, Dictionary<string, StyleEntry> byContext) {
    public static readonly State Empty = new([], new Dictionary<string, StyleEntry>(StringComparer.Ordinal));

    public readonly ConcurrentDictionary<string, StyleDefinition?> Cache = new(StringComparer.Ordinal);

    public int CachedCount;

    public StyleEntry[] Sorted { get; } = sorted;

    public Dictionary<string, StyleEntry> ByContext { get; } = byContext;
  }
}
