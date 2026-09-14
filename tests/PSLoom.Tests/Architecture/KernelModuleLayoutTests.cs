// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.TestKit;

namespace PSLoom.Tests.Architecture;

public sealed class KernelModuleLayoutTests {
  private const string MODULE_NAME = "PSLoom";

  [Fact]
  public void PublishedKernelModuleShipsWarp() {
    var moduleDirectory = RepositoryLayout.GetPublishedModuleDirectory(MODULE_NAME);

    File.Exists(Path.Combine(moduleDirectory, "PSLoom.dll")).ShouldBeTrue();
    File.Exists(Path.Combine(moduleDirectory, "Warp.dll")).ShouldBeTrue();
  }

  [Fact]
  public void PublishedKernelModuleDoesNotShipPowerShellItself() {
    var moduleDirectory = RepositoryLayout.GetPublishedModuleDirectory(MODULE_NAME);

    File.Exists(Path.Combine(moduleDirectory, "System.Management.Automation.dll")).ShouldBeFalse();
  }

  [Fact]
  public void PublishedKernelManifestVersionMatchesItsFolder() {
    var moduleDirectory = RepositoryLayout.GetPublishedModuleDirectory(MODULE_NAME);
    var manifest = RepositoryLayout.ReadDataFile(Path.Combine(moduleDirectory, "PSLoom.psd1"));

    manifest["ModuleVersion"].ShouldBe(Path.GetFileName(moduleDirectory));
  }
}
