// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Dsl;

/// <summary>
///   Marks the <see cref="ScriptBlock" /> parameter whose body runs in another scope, so the loom host can validate nesting from
///   the AST before anything executes.
/// </summary>
/// <param name="scope">The <see cref="DslScope" /> type the body runs in.</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class OpensScopeAttribute(Type scope) : Attribute {
  /// <summary>
  ///   Gets the scope the body runs in.
  /// </summary>
  public Type Scope { get; } = scope;
}
