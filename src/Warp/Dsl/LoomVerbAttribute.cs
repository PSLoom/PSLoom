// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Dsl;

/// <summary>
///   Declares a verb's DSL name and the scopes it is valid in.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class LoomVerbAttribute : Attribute {
  /// <summary>
  ///   Initializes a new instance of the <see cref="LoomVerbAttribute" /> class.
  /// </summary>
  /// <param name="name">The bare word used in DSL bodies (e.g. <c>Sley</c>).</param>
  /// <param name="scopes">Every <see cref="DslScope" /> type the verb is valid in; at least one.</param>
  public LoomVerbAttribute(string name, params Type[] scopes) {
    Name = name;
    Scopes = scopes;
  }

  /// <summary>
  ///   Gets the DSL name.
  /// </summary>
  public string Name { get; }

  /// <summary>
  ///   Gets the scopes the verb is valid in.
  /// </summary>
  public IReadOnlyList<Type> Scopes { get; }

  /// <summary>
  ///   Gets or sets how <c>Invoke-Loom -Reweave</c> treats the verb. Defaults to <see cref="ReweaveBehavior.Replay" />.
  /// </summary>
  public ReweaveBehavior Reweave { get; set; } = ReweaveBehavior.Replay;
}
