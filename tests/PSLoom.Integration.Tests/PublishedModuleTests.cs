// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text.Json;
using PSLoom.Integration.Tests.Utility;
using PSLoom.TestKit;

namespace PSLoom.Integration.Tests;

/// <summary>
///   The published modules, driven from a real <c>pwsh</c> process. Everything in-process tests cannot see lives here: module
///   manifests, assembly loading across two module folders, and the engine actually exiting.
/// </summary>
public sealed class PublishedModuleTests {
  [Fact]
  public void ADraftThreadingReedCompletesThroughTabExpansion2() {
    var result = PwshProcess.Run(
      """
      Import-Module PSLoom
      Invoke-Loom {
        Thread Reed
        Treadle glog { git log --oneline }
        Sley git -Alias g {
          Command log { Option --graph }
          Command commit -Alias ci {
            Option --message -Alias '-m' { Argument text -Source { 'fix', 'feat' } }
          }
        }
      }
      function tab([string]$line) {
        @((TabExpansion2 -inputScript $line -cursorColumn $line.Length).CompletionMatches | ForEach-Object CompletionText)
      }
      [pscustomobject]@{
        errors = $Error.Count
        subcommand = tab 'git co'
        option = tab 'git commit --m'
        alias = tab 'g ci --m'
        source = tab 'git commit -m fe'
        treadle = tab 'glog --g'
      } | ConvertTo-Json -Compress
      """);

    var json = result.Json();

    json.GetProperty("errors").GetInt32().ShouldBe(0, result.Error);
    Strings(json, "subcommand").ShouldBe(["commit"]);
    Strings(json, "option").ShouldBe(["--message"]);
    Strings(json, "alias").ShouldBe(["--message"]);
    Strings(json, "source").ShouldBe(["feat"]);
    Strings(json, "treadle").ShouldBe(["--graph"]);
  }

  [Theory]
  [InlineData("PSLoom")]
  [InlineData("PSLoom.Reed")]
  public void EveryExportedCmdletIsTheManifestsList(string module) {
    var manifest = RepositoryLayout.ReadDataFile(Path.Combine(RepositoryLayout.GetPublishedModuleDirectory(module), $"{module}.psd1"));
    var declared = ((object[])manifest["CmdletsToExport"]!).Cast<string>().Order(StringComparer.OrdinalIgnoreCase);

    var result = PwshProcess.Run(
      $"""
       Import-Module PSLoom
       Import-Module {module}
       ConvertTo-Json -Compress -InputObject @(Get-Command -Module {module} -CommandType Cmdlet | ForEach-Object Name | Sort-Object)
       """);

    result.Json().EnumerateArray().Select(name => name.GetString()!).Order(StringComparer.OrdinalIgnoreCase).ShouldBe(declared, result.Error);
  }

  [Fact]
  public void OnlyTheKernelsCopyOfWarpIsLoaded() {
    var result = PwshProcess.Run(
      """
      Import-Module PSLoom
      Invoke-Loom { Thread Reed }
      $warp = @([AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq 'Warp' })
      [pscustomobject]@{
        count = $warp.Count
        location = Split-Path -Parent $warp[0].Location
        kernel = (Get-Module PSLoom).ModuleBase
        reed = [bool](Get-Module PSLoom.Reed)
      } | ConvertTo-Json -Compress
      """);

    var json = result.Json();

    json.GetProperty("reed").GetBoolean().ShouldBeTrue(result.Error);
    json.GetProperty("count").GetInt32().ShouldBe(1);
    Path.GetFullPath(json.GetProperty("location").GetString()!).TrimEnd(Path.DirectorySeparatorChar)
      .ShouldBe(Path.GetFullPath(json.GetProperty("kernel").GetString()!).TrimEnd(Path.DirectorySeparatorChar));
  }

  [Fact]
  public void SessionExitingFiresWhenTheEngineExits() {
    var marker = Path.Combine(Path.GetTempPath(), $"psloom-exiting-{Guid.NewGuid():N}.txt");

    try {
      // No wiring call: importing the kernel is enough, and the hook must run as the process shuts down. The handler writes through
      // .NET on purpose: by then the runspace is Closing, so PowerShell can no longer auto-load a module such as the one holding
      // Set-Content — true of any Exiting handler, PSLoom's or not.
      var result = PwshProcess.Run(
        $$"""
          Import-Module PSLoom
          Register-Hook SessionExiting { [System.IO.File]::WriteAllText('{{marker}}', 'exited') } | Out-Null
          exit 0
          """);

      result.ExitCode.ShouldBe(0, result.Error);
      File.Exists(marker).ShouldBeTrue();
    }
    finally {
      File.Delete(marker);
    }
  }

  [Fact]
  public void AFailingVerbNeverTearsDownTheCallersScript() {
    var result = PwshProcess.Run(
      """
      Import-Module PSLoom
      Invoke-Loom {
        Treadle 'not a name' { git log }
        Style 'app:*' 'color' 'Cyan'
      }
      [pscustomobject]@{
        errors = @($Error | ForEach-Object FullyQualifiedErrorId)
        color = Get-Style 'app:main' 'color'
        reached = $true
      } | ConvertTo-Json -Compress
      """);

    var json = result.Json();

    json.GetProperty("reached").GetBoolean().ShouldBeTrue();
    json.GetProperty("color").GetString().ShouldBe("Cyan");
    Strings(json, "errors").ShouldHaveSingleItem().ShouldStartWith("TREADLE_INVALID_NAME");
  }

  private static IReadOnlyList<string> Strings(JsonElement json, string property)
    => json.GetProperty(property) is { ValueKind: JsonValueKind.Array } array
      ? [.. array.EnumerateArray().Select(item => item.GetString()!)]
      : [json.GetProperty(property).GetString()!];
}
