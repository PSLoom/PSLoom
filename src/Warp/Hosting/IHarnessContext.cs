// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Dsl;

namespace PSLoom.Warp.Hosting;

/// <summary>
///   Runtime access to a composed harness's capabilities, for its cmdlet surface (code that runs outside a draft).
/// </summary>
public interface IHarnessContext : IHarnessServices {
  /// <summary>
  ///   Gets the runner that executes a DSL body outside <c>Invoke-Loom</c>, with the same scope machinery.
  /// </summary>
  IDslRunner Dsl { get; }
}
