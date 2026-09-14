// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;

namespace PSLoom.Warp.Hosting;

/// <summary>
///   Lets a harness offer a typed API to other harnesses, and consume one only when its owner happens to be loaded.
/// </summary>
public interface IHarnessDirectory {
  /// <summary>
  ///   Publishes an API under its interface type. A second publication of the same type by another harness is an error naming
  ///   both owners.
  /// </summary>
  /// <typeparam name="TApi">The public interface other harnesses look up.</typeparam>
  /// <param name="api">The implementation.</param>
  void Publish<TApi>(TApi api) where TApi : class;

  /// <summary>
  ///   Looks up an API published by another harness in this runspace.
  /// </summary>
  /// <typeparam name="TApi">The interface to look up.</typeparam>
  /// <param name="api">The implementation, when present.</param>
  /// <returns><see langword="true" /> when a harness in this runspace published <typeparamref name="TApi" />.</returns>
  bool TryGet<TApi>([NotNullWhen(true)] out TApi? api) where TApi : class;
}
