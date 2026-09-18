// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Hosting;

/// <summary>
///   Declares a harness's identity. Identity is a compile-time constant: there is nothing to mutate or re-check later.
/// </summary>
/// <param name="name">The harness name, as written after <c>Thread</c> (module <c>PSLoom.&lt;name&gt;</c>).</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class HarnessAttribute(string name) : Attribute {
  /// <summary>
  ///   Gets the harness name.
  /// </summary>
  public string Name { get; } = name;

  /// <summary>
  ///   Gets or sets a one-line description.
  /// </summary>
  public string? Description { get; set; }
}
