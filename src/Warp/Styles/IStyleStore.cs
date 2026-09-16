// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace PSLoom.Warp.Styles;

/// <summary>
///   The <c>zstyle</c>-like store: values defined for wildcard context patterns, resolved for concrete contexts. Among matching
///   definitions the longest literal (non-wildcard) prefix wins; ties go to the most recent definition. Matching is ordinal and
///   case-sensitive.
/// </summary>
public interface IStyleStore {
  /// <summary>
  ///   Resolves the winning definition for a concrete context. Never runs user code.
  /// </summary>
  /// <param name="context">The concrete context (not a pattern).</param>
  /// <param name="name">The style name.</param>
  /// <returns>The winning definition, or <see langword="null" /> when no pattern matches.</returns>
  StyleDefinition? Resolve(string context, string name);

  /// <summary>
  ///   Resolves and converts a value with PowerShell's conversion rules.
  /// </summary>
  /// <typeparam name="T">The target type.</typeparam>
  /// <param name="context">The concrete context.</param>
  /// <param name="name">The style name.</param>
  /// <param name="value">The converted value, when resolved and convertible.</param>
  /// <returns><see langword="true" /> when a definition matched and its value converted to <typeparamref name="T" />.</returns>
  bool TryGet<T>(string context, string name, [MaybeNullWhen(false)] out T value) {
    if (Resolve(context, name) is { } definition &&
        LanguagePrimitives.TryConvertTo(definition.Value, out T? converted)) {
      value = converted!;
      return true;
    }

    value = default;
    return false;
  }

  /// <summary>
  ///   Defines or redefines the value for an exact context pattern and name, then runs affected watchers.
  /// </summary>
  /// <param name="context">The context pattern (PowerShell wildcard syntax).</param>
  /// <param name="name">The style name.</param>
  /// <param name="value">The value; a <c>PSObject</c> is unwrapped to its base object.</param>
  /// <returns>The outcome, including every watcher failure.</returns>
  StyleWriteResult Set(string context, string name, object? value);

  /// <summary>
  ///   Removes the definition for an exact context pattern and name, then runs affected watchers.
  /// </summary>
  /// <param name="context">The exact context pattern the definition was set under.</param>
  /// <param name="name">The style name.</param>
  /// <returns>The outcome; <see cref="StyleWriteResult.Applied" /> is <see langword="false" /> when nothing was defined.</returns>
  StyleWriteResult Remove(string context, string name);

  /// <summary>
  ///   Watches what a concrete context resolves to. Fires only when the winning definition changes (removals included).
  /// </summary>
  /// <param name="context">The concrete context.</param>
  /// <param name="name">The style name.</param>
  /// <param name="watcher">The callback.</param>
  /// <param name="replay">Deliver the current resolved value immediately, with a <see langword="null" /> old value.</param>
  /// <returns>A handle that stops watching when disposed.</returns>
  IDisposable Watch(string context, string name, StyleWatcher watcher, bool replay = false);

  /// <summary>
  ///   Watches writes whose context pattern matches <paramref name="contextPattern" />. Coarser than
  ///   <see cref="Watch" />: fires on a matching write even if nothing resolves differently.
  /// </summary>
  /// <param name="contextPattern">The pattern the written context is matched against.</param>
  /// <param name="name">The style name.</param>
  /// <param name="watcher">The callback.</param>
  /// <param name="replay">
  ///   Deliver, immediately, one change per stored definition whose context matches the pattern, with a <see langword="null" /> old
  ///   value — the writes the watcher would have seen had it been registered first.
  /// </param>
  /// <returns>A handle that stops watching when disposed.</returns>
  IDisposable WatchPattern(string contextPattern, string name, StyleWatcher watcher, bool replay = false);
}
