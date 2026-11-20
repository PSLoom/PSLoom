// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.ManifestTool.Tests.Samples;
using PSLoom.TestKit;

namespace PSLoom.ManifestTool.Tests;

public sealed class ManifestCommandTests {
  private const string TEMPLATE = """
    @{
      RootModule = 'Sample.dll'
      ModuleVersion = '$version$'
      GUID = '0b2a5c0e-3f0b-4d55-9a53-6f1f0c9f6b11'
      Author = 'Sample'
      Description = 'Sample'
      CmdletsToExport = @($cmdlets$)
      FunctionsToExport = @()
      AliasesToExport = @()
      VariablesToExport = @()
      RequiredModules = @($requiredModules$)
      PrivateData = @{
        PSData = @{
          $prerelease$
        }
      }
    }
    """;

  [Fact]
  public void RunWritesAManifestThatPowerShellAccepts() {
    var directory = Directory.CreateTempSubdirectory("psloom-manifest-");

    try {
      var module = Directory.CreateDirectory(Path.Combine(directory.FullName, "Sample")).FullName;
      var assembly = Path.Combine(module, "Sample.dll");
      var template = Path.Combine(directory.FullName, "Sample.psd1");
      var output = Path.Combine(module, "Sample.psd1");
      File.Copy(typeof(GetSampleCmdlet).Assembly.Location, assembly);
      File.WriteAllText(template, TEMPLATE);
      using var error = new StringWriter();

      var exitCode =
        ManifestCommand.Run(
          ["--assembly", assembly, "--template", template, "--output", output, "--version", "0.1.0-alpha.1"], error);

      Assert.Equal(string.Empty, error.ToString());
      Assert.Equal(0, exitCode);

      var quoted = output.Replace("'", "''", StringComparison.Ordinal);
      var script = "$m = Test-ModuleManifest -Path '" + quoted + "' -ErrorAction Stop\n" +
                   "\"$($m.Version)|$($m.PrivateData.PSData.Prerelease)|$($m.ExportedCmdlets.Keys -join ',')\"";
      var result = PwshProcess.Run(script);

      Assert.Equal(0, result.ExitCode);
      Assert.Equal("0.1.0|alpha1|Get-Sample,Invoke-Sample", result.StandardOutput.Trim());
    }
    finally {
      directory.Delete(true);
    }
  }

  [Fact]
  public void RunReportsAMissingOption() {
    using var error = new StringWriter();

    var exitCode = ManifestCommand.Run(["--assembly", "x.dll"], error);

    Assert.Equal(1, exitCode);
    Assert.Contains("--template", error.ToString(), StringComparison.Ordinal);
  }

  [Fact]
  public void RunReportsAnUnknownOption() {
    using var error = new StringWriter();

    var exitCode = ManifestCommand.Run(["--nope", "x"], error);

    Assert.Equal(1, exitCode);
    Assert.Contains("--nope", error.ToString(), StringComparison.Ordinal);
  }
}
