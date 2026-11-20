// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.IO.Compression;
using PSLoom.TestKit;

namespace PSLoom.Tests.Eng;

public sealed class NewModulePackageTests {
  private readonly static string Root = RepoLayout.FindRoot();
  private readonly static string Script = Path.Combine(Root, "eng", "New-ModulePackage.ps1");

  [Fact]
  public void TheBuiltModuleBecomesOnePackageNamedAfterItsVersion() {
    var directory = Directory.CreateTempSubdirectory("psloom-package-");

    try {
      var result = PwshProcess.RunFile(Script, ["-Path", Root, "-Destination", directory.FullName]);

      Assert.Equal(0, result.ExitCode);

      var package = Assert.Single(Directory.GetFiles(directory.FullName, "*.nupkg"));

      Assert.Matches(@"^PSLoom\.\d+\.\d+\.\d+(-[0-9A-Za-z]+)?\.nupkg$", Path.GetFileName(package));
    }
    finally {
      directory.Delete(true);
    }
  }

  [Fact]
  public void ThePackageHoldsTheAssemblyAndTheManifest() {
    var directory = Directory.CreateTempSubdirectory("psloom-package-");

    try {
      PwshProcess.RunFile(Script, ["-Path", Root, "-Destination", directory.FullName]);

      using var archive = ZipFile.OpenRead(Assert.Single(Directory.GetFiles(directory.FullName, "*.nupkg")));
      var entries = archive.Entries.Select(entry => entry.FullName).ToArray();

      Assert.Contains("PSLoom.dll", entries, StringComparer.Ordinal);
      Assert.Contains("PSLoom.psd1", entries, StringComparer.Ordinal);
    }
    finally {
      directory.Delete(true);
    }
  }

  [Fact]
  public void ARepositoryWithoutBuiltModulesIsAnError() {
    var directory = Directory.CreateTempSubdirectory("psloom-package-");

    try {
      var result = PwshProcess.RunFile(Script,
        ["-Path", directory.FullName, "-Destination", Path.Combine(directory.FullName, "out")]);

      Assert.NotEqual(0, result.ExitCode);
      Assert.Contains("dotnet build", result.StandardError, StringComparison.Ordinal);
    }
    finally {
      directory.Delete(true);
    }
  }
}
