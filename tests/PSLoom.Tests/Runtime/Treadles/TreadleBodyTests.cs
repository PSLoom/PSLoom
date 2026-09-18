// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation;
using JetBrains.Annotations;
using PSLoom.Runtime.Treadles;
using PSLoom.TestKit;

namespace PSLoom.Tests.Runtime.Treadles;

[TestSubject(typeof(TreadleBody))]
public sealed class TreadleBodyTests {
  [Fact]
  public void BarewordCommand_AndArguments_AreBaked() {
    var body = TreadleBody.Parse("git log --oneline");

    body.TargetCommand.ShouldBe("git");
    body.BakedTokens.ShouldBe(["log", "--oneline"]);
  }

  [Fact]
  public void Parameters_KeepTheirSpelling_AndValuesAreQuoted() {
    var body = TreadleBody.Parse("Get-ChildItem -Force 'my src'");

    body.ToWrapperScript().ShouldBe("& 'Get-ChildItem' -Force 'my src' @args");
  }

  [Fact]
  public void QuotesInsideAValue_AreEscaped() {
    var body = TreadleBody.Parse("git commit -m \"it's fine\"");

    body.BakedTokens.ShouldBe(["commit", "-m", "it's fine"]);
    body.ToWrapperScript().ShouldBe("& 'git' 'commit' -m 'it''s fine' @args");
  }

  [Fact]
  public void NumericArguments_AreBakedAsText()
    => TreadleBody.Parse("git log -n 3").BakedTokens.ShouldBe(["log", "-n", "3"]);

  [Fact]
  public void QuotedCommandName_IsAccepted()
    => TreadleBody.Parse(@"& 'C:\tools\my tool.exe' --fast").TargetCommand.ShouldBe(@"C:\tools\my tool.exe");

  [Fact]
  public void AScriptBlock_ParsesLikeItsText() {
    using var runspace = PowerShellHost.CreateRunspace();
    using var _ = PowerShellHost.UseAsDefault(runspace);

    var body = TreadleBody.Parse(ScriptBlock.Create("git log --oneline --graph"));

    body.ToString().ShouldBe("git log --oneline --graph");
  }

  [Theory]
  [InlineData("", "empty")]
  [InlineData("git log\ngit status", "more than one statement")]
  [InlineData("git log | Select-Object -First 1", "single command")]
  [InlineData("git log > out.txt", "redirects")]
  [InlineData("param($Branch) git log", "parameters")]
  [InlineData("& $command log", "not a literal")]
  [InlineData("git log $branch", "not a constant")]
  [InlineData("git log $(Get-Location)", "not a constant")]
  [InlineData("git log \"$env:USERNAME\"", "not a constant")]
  [InlineData("git log { Get-Date }", "not a constant")]
  [InlineData("git log (Get-Location)", "not a constant")]
  [InlineData("if ($true) { git log }", "single command")]
  public void RejectedBodies_ExplainWhy(string text, string expected) {
    var exception = Should.Throw<TreadleException>(() => TreadleBody.Parse(text));

    exception.ErrorId.ShouldBe(TreadleException.BODY_NOT_SINGLE_COMMAND);
    exception.Message.ShouldContain(expected);
  }
}
