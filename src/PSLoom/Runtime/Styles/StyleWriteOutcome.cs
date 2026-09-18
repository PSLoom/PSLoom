// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Styles;

namespace PSLoom.Runtime.Styles;

/// <summary>
///   The detailed result of a write, for cmdlet narration. <see cref="IStyleStore" /> callers get a <see cref="StyleWriteResult" />.
/// </summary>
internal sealed class StyleWriteOutcome(bool applied,
  StyleDefinition? previous,
  StyleDefinition? current,
  IReadOnlyList<StyleWatcherOutcome> watchers
) {
  public bool Applied { get; } = applied;

  /// <summary>
  ///   Gets the definition previously stored under the exact context pattern.
  /// </summary>
  public StyleDefinition? Previous { get; } = previous;

  /// <summary>
  ///   Gets the definition written; <see langword="null" /> for a removal.
  /// </summary>
  public StyleDefinition? Current { get; } = current;

  public IReadOnlyList<StyleWatcherOutcome> Watchers { get; } = watchers;

  public StyleWriteResult ToResult() {
    List<Exception>? failures = null;

    foreach (var watcher in Watchers) {
      if (watcher.Exception is { } exception) {
        (failures ??= []).Add(exception);
      }
    }

    return new StyleWriteResult(Applied, failures ?? []);
  }
}
