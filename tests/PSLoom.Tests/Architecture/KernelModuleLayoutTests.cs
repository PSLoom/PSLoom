// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation;
using System.Reflection;
using PSLoom.Runtime;
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

  [Fact]
  public void PublishedKernelManifestExportsEveryCmdletExactly() {
    var moduleDirectory = RepositoryLayout.GetPublishedModuleDirectory(MODULE_NAME);
    var manifest = RepositoryLayout.ReadDataFile(Path.Combine(moduleDirectory, "PSLoom.psd1"));
    var exported = ((object[])manifest["CmdletsToExport"]!).Cast<string>().Order(StringComparer.OrdinalIgnoreCase);

    var implemented = typeof(KernelException).Assembly.GetTypes()
      .Select(type => type.GetCustomAttribute<CmdletAttribute>())
      .OfType<CmdletAttribute>()
      .Select(cmdlet => $"{cmdlet.VerbName}-{cmdlet.NounName}")
      .Order(StringComparer.OrdinalIgnoreCase);

    exported.ShouldBe(implemented);
  }
}
