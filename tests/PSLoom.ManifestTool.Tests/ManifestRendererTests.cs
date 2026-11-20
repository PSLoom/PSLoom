// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.ManifestTool.Tests;

public sealed class ManifestRendererTests {
  private const string TEMPLATE = "v=$version$|c=$cmdlets$|r=$requiredModules$|p=$prerelease$";

  [Fact]
  public void RenderFillsEveryToken() {
    const string EXPECTED =
      "v=0.1.0|c='Get-Sample', 'Invoke-Sample'|r=@{ ModuleName = 'PSLoom'; ModuleVersion = '0.1.0' }|p=Prerelease = 'alpha1'";

    var manifest = ManifestRenderer.Render(TEMPLATE, ModuleVersion.Parse("0.1.0-alpha.1"),
      ["Get-Sample", "Invoke-Sample"], [new RequiredModule("PSLoom", "0.1.0")]);

    Assert.Equal(EXPECTED, manifest);
  }

  [Fact]
  public void RenderLeavesOptionalPartsEmpty() {
    var manifest = ManifestRenderer.Render(TEMPLATE, ModuleVersion.Parse("0.1.0"), [], []);

    Assert.Equal("v=0.1.0|c=|r=|p=", manifest);
  }

  [Fact]
  public void RenderEscapesSingleQuotes() {
    var manifest = ManifestRenderer.Render(TEMPLATE, ModuleVersion.Parse("0.1.0"), ["Get-It's"], []);

    Assert.Contains("'Get-It''s'", manifest, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData("$version$")]
  [InlineData("$cmdlets$")]
  [InlineData("$requiredModules$")]
  [InlineData("$prerelease$")]
  public void RenderRejectsATemplateWithoutAToken(string token) {
    var template = TEMPLATE.Replace(token, string.Empty, StringComparison.Ordinal);

    var exception =
      Assert.Throws<InvalidOperationException>(()
        => ManifestRenderer.Render(template, ModuleVersion.Parse("0.1.0"), [], []));

    Assert.Contains(token, exception.Message, StringComparison.Ordinal);
  }
}
