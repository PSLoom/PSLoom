// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Treadles;

/// <summary>
///   A treadle: <c>Name</c> runs <c>TargetCommand</c> with <c>BakedTokens</c> followed by whatever the user typed.
/// </summary>
public sealed class TreadleDefinition {
  internal TreadleDefinition(string name, string targetCommand, IReadOnlyList<string> bakedTokens) {
    Name = name;
    TargetCommand = targetCommand;
    BakedTokens = bakedTokens;
  }

  /// <summary>
  ///   Gets the treadle name.
  /// </summary>
  public string Name { get; }

  /// <summary>
  ///   Gets the command the treadle invokes.
  /// </summary>
  public string TargetCommand { get; }

  /// <summary>
  ///   Gets the constant argument tokens placed before the user's arguments.
  /// </summary>
  public IReadOnlyList<string> BakedTokens { get; }
}