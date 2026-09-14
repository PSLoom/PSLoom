// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Dsl;

/// <summary>
///   Contributes verbs to DSL scopes.
/// </summary>
public interface IVerbRegistry {
  /// <summary>
  ///   Registers a verb in every scope its <see cref="LoomVerbAttribute" /> lists. Attribute metadata is read once, here.
  /// </summary>
  /// <typeparam name="TVerb">The verb type.</typeparam>
  /// <exception cref="PowerShellException">
  ///   The attribute is missing or invalid; a scope in <see cref="LoomVerbAttribute.Scopes" /> or
  ///   <see cref="OpensScopeAttribute.Scope" /> is not a valid <see cref="DslScope" /> (<c>WARP_SCOPE_INVALID</c>); or another
  ///   owner already registered the same name in one of the scopes (the error names both owners).
  /// </exception>
  void Add<TVerb>() where TVerb : LoomVerb, new();
}
