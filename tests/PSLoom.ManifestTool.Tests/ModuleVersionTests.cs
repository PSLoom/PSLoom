// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.ManifestTool.Tests;

public sealed class ModuleVersionTests {
  [Theory]
  [InlineData("0.1.0", "0.1.0", null)]
  [InlineData("0.1.0-alpha.1", "0.1.0", "alpha1")]
  [InlineData("1.2.3-rc.2+build5", "1.2.3", "rc2")]
  [InlineData("1.2.3+build5", "1.2.3", null)]
  public void ParseSplitsTheNumericPartFromThePrereleaseLabel(string input, string numeric, string? prerelease) {
    var version = ModuleVersion.Parse(input);

    Assert.Equal(numeric, version.Numeric);
    Assert.Equal(prerelease, version.Prerelease);
  }

  [Theory]
  [InlineData("1.2")]
  [InlineData("1.2.3.4")]
  [InlineData("1.2.3-")]
  [InlineData("one.two.three")]
  public void ParseRejectsWhatIsNotMajorMinorPatch(string input) {
    Assert.Throws<FormatException>(() => ModuleVersion.Parse(input));
  }
}
