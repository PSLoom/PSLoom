// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Styles;

namespace PSLoom.Runtime.Styles;

/// <summary>
///   A stored definition paired with its compiled context matcher.
/// </summary>
internal sealed class StyleEntry(StyleDefinition definition, ContextMatcher matcher) {
  public StyleDefinition Definition { get; } = definition;

  public ContextMatcher Matcher { get; } = matcher;
}
