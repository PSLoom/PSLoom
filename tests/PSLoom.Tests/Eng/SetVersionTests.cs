// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.TestKit;

namespace PSLoom.Tests.Eng;

public sealed class SetVersionTests {
  private readonly static string Script = Path.Combine(RepoLayout.FindRoot(), "eng", "Set-Version.ps1");

  [Theory]
  [InlineData("1.2.3")]
  [InlineData("0.1.0-alpha.1")]
  public void TheVersionIsWrittenToVersionProps(string version) {
    var directory = Directory.CreateTempSubdirectory("psloom-version-");

    try {
      var result = PwshProcess.RunFile(Script, ["-Version", version, "-Path", directory.FullName]);

      Assert.Equal(0, result.ExitCode);
      Assert.Contains($"<Version>{version}</Version>",
        File.ReadAllText(Path.Combine(directory.FullName, "Version.props")), StringComparison.Ordinal);
    }
    finally {
      directory.Delete(true);
    }
  }

  [Theory]
  [InlineData("1.2")]
  [InlineData("v1.2.3")]
  [InlineData("1.2.3-")]
  public void AMalformedVersionIsRejectedAndNothingIsWritten(string version) {
    var directory = Directory.CreateTempSubdirectory("psloom-version-");

    try {
      var result = PwshProcess.RunFile(Script, ["-Version", version, "-Path", directory.FullName]);

      Assert.NotEqual(0, result.ExitCode);
      Assert.False(File.Exists(Path.Combine(directory.FullName, "Version.props")));
    }
    finally {
      directory.Delete(true);
    }
  }
}
