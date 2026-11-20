// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.TestKit;

namespace PSLoom.Tests.Eng;

public sealed class SyncCommonTests {
  private const string CONFIG = "@{ Kind = 'Base'; Owner = 'Acme'; Repo = 'Widget'; Title = 'Widget' }";

  private readonly static string Root = RepoLayout.FindRoot();
  private readonly static string Script = Path.Combine(Root, "eng", "Sync-Common.ps1");

  [Fact]
  public void SyncWritesTheCanonicalFiles() {
    var directory = Directory.CreateTempSubdirectory("psloom-sync-");

    try {
      File.WriteAllText(Path.Combine(directory.FullName, "psloom.sync.psd1"), CONFIG);

      var result = PwshProcess.RunFile(Script, ["-Path", directory.FullName]);

      Assert.Equal(0, result.ExitCode);
      Assert.Equal(Normalize(File.ReadAllText(Path.Combine(Root, "eng", "common", "files", ".editorconfig"))),
        Normalize(File.ReadAllText(Path.Combine(directory.FullName, ".editorconfig"))));
    }
    finally {
      directory.Delete(true);
    }
  }

  [Fact]
  public void CheckPassesRightAfterASync() {
    var directory = Directory.CreateTempSubdirectory("psloom-sync-");

    try {
      File.WriteAllText(Path.Combine(directory.FullName, "psloom.sync.psd1"), CONFIG);
      PwshProcess.RunFile(Script, ["-Path", directory.FullName]);

      var result = PwshProcess.RunFile(Script, ["-Path", directory.FullName, "-Check"]);

      Assert.Equal(0, result.ExitCode);
    }
    finally {
      directory.Delete(true);
    }
  }

  [Fact]
  public void CheckNamesTheFileThatDriftedAndLeavesItAlone() {
    var directory = Directory.CreateTempSubdirectory("psloom-sync-");

    try {
      File.WriteAllText(Path.Combine(directory.FullName, "psloom.sync.psd1"), CONFIG);
      PwshProcess.RunFile(Script, ["-Path", directory.FullName]);
      var drifted = Path.Combine(directory.FullName, ".editorconfig");
      File.WriteAllText(drifted, "changed");

      var result = PwshProcess.RunFile(Script, ["-Path", directory.FullName, "-Check"]);

      Assert.Equal(1, result.ExitCode);
      Assert.Contains("Out of sync: .editorconfig", result.StandardOutput, StringComparison.Ordinal);
      Assert.Equal("changed", File.ReadAllText(drifted));
    }
    finally {
      directory.Delete(true);
    }
  }

  [Fact]
  public void SyncFailsWithoutARepositoryConfiguration() {
    var directory = Directory.CreateTempSubdirectory("psloom-sync-");

    try {
      var result = PwshProcess.RunFile(Script, ["-Path", directory.FullName]);

      Assert.NotEqual(0, result.ExitCode);
      Assert.Contains("psloom.sync.psd1", result.StandardError, StringComparison.Ordinal);
    }
    finally {
      directory.Delete(true);
    }
  }

  [Fact]
  public void TheRepositoryItselfIsInSync() {
    var result = PwshProcess.RunFile(Script, ["-Path", Root, "-Check"]);

    Assert.True(result.ExitCode == 0, result.StandardOutput);
  }

  [Fact]
  public void TemplatesGetTheValuesOfTheRepository() {
    var directory = Directory.CreateTempSubdirectory("psloom-sync-");

    try {
      File.WriteAllText(Path.Combine(directory.FullName, "psloom.sync.psd1"), CONFIG);

      var result = PwshProcess.RunFile(Script, ["-Path", directory.FullName]);
      var cliff = File.ReadAllText(Path.Combine(directory.FullName, "cliff.toml"));

      Assert.Equal(0, result.ExitCode);
      Assert.Contains("owner = \"Acme\"", cliff, StringComparison.Ordinal);
      Assert.Contains("repo = \"Widget\"", cliff, StringComparison.Ordinal);
      Assert.Contains("# Widget Changelog", cliff, StringComparison.Ordinal);
      Assert.DoesNotContain("{{Owner}}", cliff, StringComparison.Ordinal);
      Assert.DoesNotContain("{{Repo}}", cliff, StringComparison.Ordinal);
      Assert.DoesNotContain("{{Title}}", cliff, StringComparison.Ordinal);
    }
    finally {
      directory.Delete(true);
    }
  }

  [Theory]
  [InlineData("ci.yml")]
  [InlineData("prepare-release.yml")]
  [InlineData("release.yml")]
  public void WorkflowsAreWrittenUnderDotGithub(string workflow) {
    var directory = Directory.CreateTempSubdirectory("psloom-sync-");

    try {
      File.WriteAllText(Path.Combine(directory.FullName, "psloom.sync.psd1"), CONFIG);

      PwshProcess.RunFile(Script, ["-Path", directory.FullName]);

      Assert.True(File.Exists(Path.Combine(directory.FullName, ".github", "workflows", workflow)));
    }
    finally {
      directory.Delete(true);
    }
  }

  private static string Normalize(string text)
    => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
