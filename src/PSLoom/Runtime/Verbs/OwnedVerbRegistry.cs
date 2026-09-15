// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Dsl;

namespace PSLoom.Runtime.Verbs;

/// <summary>
///   The <see cref="IVerbRegistry" /> handed to one harness: every verb it adds is owned by that harness.
/// </summary>
internal sealed class OwnedVerbRegistry(VerbRegistry registry, string owner) : IVerbRegistry {
  /// <inheritdoc />
  public void Add<TVerb>() where TVerb : LoomVerb, new()
    => registry.Add(typeof(TVerb), owner);
}
