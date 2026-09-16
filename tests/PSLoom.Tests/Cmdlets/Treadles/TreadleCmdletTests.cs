// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Cmdlets.Treadles;
using PSLoom.Runtime.Treadles;
using PSLoom.Tests.Utility;
using PSLoom.Warp.Treadles;

namespace PSLoom.Tests.Cmdlets.Treadles;

[TestSubject(typeof(NewTreadleCmdlet))]
[TestSubject(typeof(GetTreadleCmdlet))]
[TestSubject(typeof(RemoveTreadleCmdlet))]
public sealed class TreadleCmdletTests {
  [Fact]
  public void NewTreadle_InstallsAFunction_ThatAppendsTheCallersArguments() {
    using var session = new KernelSession();

    session.Run("New-Treadle echoArgs { Write-Output 'first' }");

    session.Run("echoArgs second third").Select(result => result.BaseObject).ShouldBe(["first", "second", "third"]);
    session.Streams.Error.ShouldBeEmpty();
  }

  [Fact]
  public void BakedParameters_BindAsParameters_NotAsPositionalText() {
    using var session = new KernelSession();
    session.Run("function Probe { param([switch]$Loud, [string]$Value) if ($Loud) { $Value.ToUpper() } else { $Value } }");

    session.Run("New-Treadle shout { Probe -Loud }");

    session.Run("shout -Value hi").Single().BaseObject.ShouldBe("HI");
  }

  [Fact]
  public void AStringCommand_IsAcceptedLikeAScriptBlock() {
    using var session = new KernelSession();

    session.Run("New-Treadle echoArgs 'Write-Output'");

    session.Run("echoArgs only").Single().BaseObject.ShouldBe("only");
  }

  [Fact]
  public void AnExistingCommand_CollidesUntilForced() {
    using var session = new KernelSession();
    session.Run("function taken { 'original' }");

    session.Run("New-Treadle taken { Write-Output 'treadle' }");

    var error = session.Streams.Error.ShouldHaveSingleItem();
    error.FullyQualifiedErrorId.ShouldStartWith(TreadleException.NAME_COLLISION);
    error.Exception.Message.ShouldContain("function");
    session.Run("taken").Single().BaseObject.ShouldBe("original");

    session.Run("New-Treadle taken { Write-Output 'treadle' } -Force");
    session.Run("taken").Single().BaseObject.ShouldBe("treadle");
  }

  [Theory]
  [InlineData("Set-Alias taken Get-Date", "alias")]
  [InlineData("", "cmdlet")]
  public void CollisionsNameTheCommandInTheWay(string setup, string expected) {
    using var session = new KernelSession();
    var name = setup.Length == 0 ? "Get-Style" : "taken";
    session.Run(setup.Length == 0 ? "$null = $null" : setup);

    session.Run($"New-Treadle {name} {{ Write-Output 'treadle' }}");

    session.Streams.Error.ShouldHaveSingleItem().Exception.Message.ShouldContain(expected);
  }

  [Fact]
  public void ATreadleReplacesItself_WithoutForce() {
    using var session = new KernelSession();
    session.Run("New-Treadle echoArgs { Write-Output 'first' }");

    session.Run("New-Treadle echoArgs { Write-Output 'second' }");

    session.Streams.Error.ShouldBeEmpty();
    session.Run("echoArgs").Single().BaseObject.ShouldBe("second");
  }

  [Fact]
  public void AnUnusableName_IsRejected() {
    using var session = new KernelSession();

    session.Run("New-Treadle 'two words' { Write-Output 'x' }");

    session.Streams.Error.ShouldHaveSingleItem().FullyQualifiedErrorId.ShouldStartWith(TreadleException.INVALID_NAME);
  }

  [Fact]
  public void PassThru_WritesTheDefinition() {
    using var session = new KernelSession();

    var definition = (TreadleDefinition)session.Run("New-Treadle glog { git log --oneline } -PassThru").Single().BaseObject;

    definition.Name.ShouldBe("glog");
    definition.TargetCommand.ShouldBe("git");
    definition.BakedTokens.ShouldBe(["log", "--oneline"]);
  }

  [Fact]
  public void GetTreadle_ListsEverything_OrMatchesNames() {
    using var session = new KernelSession();
    session.Run("New-Treadle glog { git log }; New-Treadle gst { git status }; New-Treadle ll { Get-ChildItem -Force }");

    Names(session, "Get-Treadle").ShouldBe(["glog", "gst", "ll"]);
    Names(session, "Get-Treadle g*").ShouldBe(["glog", "gst"]);
    Names(session, "Get-Treadle ll").ShouldBe(["ll"]);
    Names(session, "Get-Treadle nope").ShouldBeEmpty();
  }

  [Fact]
  public void RemoveTreadle_RemovesTheFunction_AndWarnsWhenUnknown() {
    using var session = new KernelSession();
    session.Run("New-Treadle glog { git log }");

    session.Run("Remove-Treadle glog");

    session.Run("Test-Path function:glog").Single().BaseObject.ShouldBe(false);
    Names(session, "Get-Treadle").ShouldBeEmpty();

    session.Run("Remove-Treadle glog");
    session.Streams.Warning.ShouldHaveSingleItem().Message.ShouldContain("glog");
    session.Streams.Error.ShouldBeEmpty();
  }

  [Fact]
  public void RemoveTreadle_LeavesAForeignFunctionAlone() {
    using var session = new KernelSession();
    session.Run("function taken { 'original' }");

    session.Run("Remove-Treadle taken");

    session.Run("taken").Single().BaseObject.ShouldBe("original");
  }

  [Fact]
  public void AHarnessSeesTheCatalog() {
    using var session = new KernelSession();
    session.Run("Invoke-Loom { Thread Fixture }");

    session.Run("New-Treadle glog { git log }");
    session.Style("fixture:treadles", "last").ShouldBe("glog");

    session.Run("Remove-Treadle glog");
    session.Style("fixture:treadles", "last").ShouldBe("<removed glog>");
  }

  private static IEnumerable<string?> Names(KernelSession session, string script)
    => session.Run(script).Select(result => ((TreadleDefinition)result.BaseObject).Name);
}
