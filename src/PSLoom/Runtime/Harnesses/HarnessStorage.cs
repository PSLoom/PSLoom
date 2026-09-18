// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Storage;

namespace PSLoom.Runtime.Harnesses;

/// <summary>
///   A harness's directory under the Creel. <see cref="IHarnessStorage.Resolve" /> comes from the contract.
/// </summary>
internal sealed class HarnessStorage(CreelPath root) : IHarnessStorage {
  /// <inheritdoc />
  public CreelPath Root { get; } = root;
}
