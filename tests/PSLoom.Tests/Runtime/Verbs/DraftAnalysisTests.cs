// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Language;
using JetBrains.Annotations;
using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Verbs;
using PSLoom.Tests.Utility;

namespace PSLoom.Tests.Runtime.Verbs;

[TestSubject(typeof(DraftAnalyzer))]
[TestSubject(typeof(DraftValidator))]
public sealed class DraftAnalysisTests {
  [Fact]
  public void FindThreads_TopLevelLiteralNames_InOrderWithoutDuplicates() {
    var (harnesses, errors)
      = DraftAnalyzer.FindThreads(Parse("Thread Reed\nStyle 'a:*' 'b' 1\nThread -Name 'Weft'\nthread reed\nThread -N:Colorway"));

    errors.ShouldBeEmpty();
    harnesses.ShouldBe(["Reed", "Weft", "Colorway"]);
  }

  [Theory]
  [InlineData("if ($true) { Thread Reed }")]
  [InlineData("foreach ($x in 1) { Thread Reed }")]
  [InlineData("& { Thread Reed }")]
  [InlineData("Thread Reed | Out-Null")]
  public void FindThreads_NestedOrPiped_IsNotTopLevel(string draft) {
    var (harnesses, errors) = DraftAnalyzer.FindThreads(Parse(draft));

    harnesses.ShouldBeEmpty();
    errors.ShouldHaveSingleItem().ErrorId.ShouldBe(LoomException.THREAD_NOT_TOP_LEVEL);
  }

  [Theory]
  [InlineData("Thread $name")]
  [InlineData("Thread \"PSLoom$suffix\"")]
  [InlineData("Thread (Get-Name)")]
  [InlineData("Thread")]
  [InlineData("Thread -Other Reed")]
  public void FindThreads_NonLiteralName_IsRejected(string draft) {
    var (harnesses, errors) = DraftAnalyzer.FindThreads(Parse(draft));

    harnesses.ShouldBeEmpty();
    errors.ShouldHaveSingleItem().ErrorId.ShouldBe(LoomException.THREAD_NAME_NOT_LITERAL);
  }

  [Theory]
  [InlineData("Crate a { Bottle x }")]
  [InlineData("Crate a -Body { Bottle x }")]
  [InlineData("Crate a -B:{ Bottle x }")]
  [InlineData("Crate -Sealed a { Bottle -Label x }")]
  [InlineData("Crate -Name a { 1..2 | ForEach-Object { Bottle $_ } }")]
  [InlineData("Get-Item . ; Bottle-Unrelated x")]
  public void Validate_VerbsInTheirScopes_HasNoErrors(string draft) {
    DraftValidator.Validate(Parse(draft), Registry()).ShouldBeEmpty();
  }

  [Fact]
  public void Validate_VerbOutsideItsScope_ReportsVerbScopesAndPosition() {
    var errors = DraftValidator.Validate(Parse("Style 'a:*' 'b' 1\n  Bottle stray"), Registry());

    var error = errors.ShouldHaveSingleItem();
    error.ErrorId.ShouldBe(LoomException.VERB_OUT_OF_SCOPE);
    error.Message.ShouldContain("'Bottle'");
    error.Message.ShouldContain("CrateScope");
    error.Message.ShouldContain("line 2, column 3");
  }

  [Fact]
  public void Validate_DraftVerbInsideNestedScope_IsReported() {
    var errors = DraftValidator.Validate(Parse("Crate a { Crate b { } }"), Registry());

    errors.ShouldHaveSingleItem().Message.ShouldContain("'Crate' cannot be used in scope 'CrateScope'");
  }

  [Fact]
  public void Bind_SwitchDoesNotConsumeThePositionalThatFollows() {
    var registry = Registry();
    var command = (CommandAst)Parse("Crate -Sealed box { }").Find(static node => node is CommandAst, true)!;

    var bound = DraftValidator.Bind(command, registry.FindByName("Crate")[0]);

    bound.Values.Select(parameter => parameter.Name).ShouldBe(["Name", "Body"], true);
  }

  private static ScriptBlockAst Parse(string script)
    => Parser.ParseInput(script, out _, out _);

  private static VerbRegistry Registry() {
    var registry = new VerbRegistry();
    registry.Add(typeof(PSLoom.Verbs.StyleVerb), LoomSession.KERNEL_OWNER);
    registry.Add(typeof(CrateVerb), "Crates");
    registry.Add(typeof(BottleVerb), "Crates");
    return registry;
  }
}
