// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Integration.Tests.Utility;

namespace PSLoom.Integration.Tests;

/// <summary>
///   Staged apply in a real process. The helper starts pwsh with -NonInteractive, so staged statements apply at the end of the draft.
/// </summary>
public sealed class StagedApplyTests {
  [Fact]
  public void ANonInteractiveProcessAppliesStagedStatementsBeforeInvokeLoomReturns() {
    var result = PwshProcess.Run(
      """
      Import-Module PSLoom
      Invoke-Loom {
        Shed -Slot 1a
        Set-Alias -Name psloom-staged -Value Get-Date -Scope Global
        Shed -Wait
        Style 'app:*' 'color' 'Cyan'
      }
      [pscustomobject]@{
        alias = [bool](Get-Alias psloom-staged -ErrorAction SilentlyContinue)
        color = Get-Style 'app:main' 'color'
        states = @(Get-Shed | ForEach-Object { $_.State.ToString() })
      } | ConvertTo-Json -Compress
      """);

    var json = result.Json();

    json.GetProperty("alias").GetBoolean().ShouldBeTrue(result.Error);
    json.GetProperty("color").GetString().ShouldBe("Cyan");
    json.GetProperty("states").EnumerateArray().Select(state => state.GetString()).ShouldBe(["Applied", "Applied"]);
  }
}
