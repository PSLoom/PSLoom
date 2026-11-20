// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Text.Json;
using PSLoom.TestKit;

namespace PSLoom.Tests.Module;

/// <summary>Imports the assembled module in a real pwsh process, as an installed module would be.</summary>
public sealed class ModuleImportTests {
  private readonly static string[] ModulePaths = [RepoLayout.ModulesDirectory(RepoLayout.FindRoot())];

  [Fact]
  public void ManifestIsValid() {
    var result =
      PwshProcess.Run(
        "(Test-ModuleManifest -Path (Get-Module -ListAvailable PSLoom)[0].Path -ErrorAction Stop).Version.ToString()",
        ModulePaths);

    Assert.Equal(0, result.ExitCode);
    Assert.Matches(@"^\d+\.\d+\.\d+$", result.StandardOutput.Trim());
  }

  [Fact]
  public void ImportExportsExactlyTheCmdletsOfTheAssembly() {
    var result = PwshProcess.Run("(Import-Module PSLoom -PassThru).ExportedCmdlets.Keys -join ','", ModulePaths);

    Assert.Equal(0, result.ExitCode);
    Assert.Equal("Get-Harness", result.StandardOutput.Trim());
  }

  [Fact]
  public void ImportIsInert() {
    const string SCRIPT = """
      $before = @(Get-Module).Name
      Import-Module PSLoom
      $added = @(@(Get-Module).Name | Where-Object { $_ -notin $before })
      $assemblies = @([System.AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq 'PSLoom' })
      [pscustomobject]@{
        Added       = $added
        Subscribers = @(Get-EventSubscriber).Count
        Assemblies  = $assemblies.Count
      } | ConvertTo-Json -Compress
      """;

    var result = PwshProcess.Run(SCRIPT, ModulePaths);

    Assert.Equal(0, result.ExitCode);

    using var document = JsonDocument.Parse(result.StandardOutput);
    var root = document.RootElement;
    var added = root.GetProperty("Added").EnumerateArray().Select(element => element.GetString()).ToArray();

    Assert.Equal(new[] { "PSLoom" }, added);
    Assert.Equal(0, root.GetProperty("Subscribers").GetInt32());
    Assert.Equal(1, root.GetProperty("Assemblies").GetInt32());
  }
}
