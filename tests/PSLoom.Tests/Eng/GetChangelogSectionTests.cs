// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.TestKit;

namespace PSLoom.Tests.Eng;

public sealed class GetChangelogSectionTests {
  private const string CHANGELOG = """
    # Sample Changelog

    ## [0.2.0](https://example.test/compare/v0.1.0..v0.2.0) - 2026-11-20

    ### Features

    - Second thing

    ## [0.1.0] - 2026-11-01

    ### Features

    - First thing
    """;

  private readonly static string Script = Path.Combine(RepoLayout.FindRoot(), "eng", "Get-ChangelogSection.ps1");

  [Fact]
  public void TheBodyOfTheSectionIsPrintedWithoutItsTitle() {
    var result = Run("0.2.0");

    Assert.Equal(0, result.ExitCode);
    Assert.Contains("- Second thing", result.StandardOutput, StringComparison.Ordinal);
    Assert.DoesNotContain("## [0.2.0]", result.StandardOutput, StringComparison.Ordinal);
    Assert.DoesNotContain("- First thing", result.StandardOutput, StringComparison.Ordinal);
  }

  [Fact]
  public void TheLastSectionRunsToTheEndOfTheFile() {
    var result = Run("0.1.0");

    Assert.Equal(0, result.ExitCode);
    Assert.Contains("- First thing", result.StandardOutput, StringComparison.Ordinal);
  }

  [Fact]
  public void AVersionWithoutASectionExitsWithOneAndPrintsNothing() {
    var result = Run("9.9.9");

    Assert.Equal(1, result.ExitCode);
    Assert.Equal(string.Empty, result.StandardOutput.Trim());
  }

  private static PwshResult Run(string version) {
    var directory = Directory.CreateTempSubdirectory("psloom-changelog-");

    try {
      var changelog = Path.Combine(directory.FullName, "CHANGELOG.md");
      File.WriteAllText(changelog, CHANGELOG);

      return PwshProcess.RunFile(Script, ["-Version", version, "-Path", changelog]);
    }
    finally {
      directory.Delete(true);
    }
  }
}
