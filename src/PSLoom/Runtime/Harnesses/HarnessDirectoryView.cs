// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using PSLoom.Warp.Hosting;

namespace PSLoom.Runtime.Harnesses;

/// <summary>
///   The <see cref="IHarnessDirectory" /> handed to one harness: what it publishes is owned by that harness.
/// </summary>
internal sealed class HarnessDirectoryView(HarnessDirectory directory, string owner) : IHarnessDirectory {
  /// <inheritdoc />
  public void Publish<TApi>(TApi api) where TApi : class
    => directory.Publish(api, owner);

  /// <inheritdoc />
  public bool TryGet<TApi>([NotNullWhen(true)] out TApi? api) where TApi : class
    => directory.TryGet(out api);
}
