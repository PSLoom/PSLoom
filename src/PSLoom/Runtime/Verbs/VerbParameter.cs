// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Runtime.Verbs;

/// <summary>
///   What static validation needs to know about one verb parameter.
/// </summary>
internal sealed class VerbParameter(string name, IReadOnlyList<string> aliases, int? position, bool isSwitch, Type? opensScope) {
  public string Name { get; } = name;

  public IReadOnlyList<string> Aliases { get; } = aliases;

  /// <summary>
  ///   Gets the positional index, or <see langword="null" /> for a named-only parameter.
  /// </summary>
  public int? Position { get; } = position;

  /// <summary>
  ///   Gets a value indicating whether the parameter takes no argument.
  /// </summary>
  public bool IsSwitch { get; } = isSwitch;

  /// <summary>
  ///   Gets the scope a script block bound to this parameter runs in, from <c>[OpensScope]</c>.
  /// </summary>
  public Type? OpensScope { get; } = opensScope;
}
