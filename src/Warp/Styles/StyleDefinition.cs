// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Styles;

/// <summary>
///   One stored definition. Immutable; a redefinition creates a new instance with a higher <see cref="Sequence" />.
/// </summary>
public sealed class StyleDefinition {
  internal StyleDefinition(string context, string name, object? value, long sequence) {
    Context = context;
    Name = name;
    Value = value;
    Sequence = sequence;
  }

  /// <summary>
  ///   Gets the context pattern the value was defined for.
  /// </summary>
  public string Context { get; }

  /// <summary>
  ///   Gets the style name.
  /// </summary>
  public string Name { get; }

  /// <summary>
  ///   Gets the value.
  /// </summary>
  public object? Value { get; }

  /// <summary>
  ///   Gets the store-wide write counter at definition time; breaks specificity ties.
  /// </summary>
  public long Sequence { get; }
}