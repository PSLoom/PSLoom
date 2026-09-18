// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Hosting;

namespace PSLoom.Fixture;

/// <summary>
///   Registers the fixture harness when the module is imported, the way every harness does.
/// </summary>
public sealed class FixtureModuleInitializer : IModuleAssemblyInitializer {
  /// <inheritdoc />
  public void OnImport()
    => HarnessHost.Register<FixtureHarness>();
}
