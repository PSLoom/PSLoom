// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.TestKit;

namespace PSLoom.Tests.Eng;

public sealed class EnterDevShellTests {
  private readonly static string Root = RepoLayout.FindRoot();
  private readonly static string Script = Path.Combine(Root, "eng", "Enter-DevShell.ps1");

  [Fact]
  public void TheBuiltModuleResolvesByName() {
    var result = PwshProcess.RunFile(Script, ["-Path", Root, "-Command", "(Import-Module PSLoom -PassThru).Path"]);

    Assert.Equal(0, result.ExitCode);
    Assert.StartsWith(RepoLayout.ModulesDirectory(Root), result.StandardOutput.Trim(),
      StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void ARepositoryWithoutBuiltModulesIsAnError() {
    var directory = Directory.CreateTempSubdirectory("psloom-devshell-");

    try {
      var copy = Path.Combine(directory.FullName, "Enter-DevShell.ps1");
      File.Copy(Script, copy);

      var result = PwshProcess.RunFile(copy, ["-Path", directory.FullName, "-Command", "'unreachable'"]);

      Assert.NotEqual(0, result.ExitCode);
      Assert.Contains("dotnet build", result.StandardError, StringComparison.Ordinal);
    }
    finally {
      directory.Delete(true);
    }
  }
}
