// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation;
using System.Management.Automation.Language;
using JetBrains.Annotations;
using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Sheds;
using PSLoom.Runtime.Verbs;
using PSLoom.TestKit;
using PSLoom.Verbs;

namespace PSLoom.Tests.Runtime.Sheds;

[TestSubject(typeof(ShedRewriter))]
public sealed class ShedRewriterTests {
  [Fact]
  public void ADraftWithoutDeclarationsIsReturnedAsIs() {
    using var runspace = PowerShellHost.CreateRunspace();
    using var _ = PowerShellHost.UseAsDefault(runspace);
    var draft = ScriptBlock.Create("Style 'a:*' 'b' 1");

    ShedRewriter.Rewrite(draft, []).ShouldBeSameAs(draft);
  }

  [Fact]
  public void EveryStatementKeepsItsLine() {
    const string BODY = "Style 'a:*' 'x' 0\nShed -Wait\nStyle 'a:*' 'b' 1\nShed -Lucid\nStyle 'a:*' 'c' 2\nStyle 'a:*' 'd' 3";
    var rewritten = Rewrite(BODY);
    var lines = rewritten.Ast.FindAll(node => node is CommandAst { CommandElements: [StringConstantExpressionAst { Value: "Style" }, ..] }, true)
      .Select(node => node.Extent.StartLineNumber);

    // 'Style … b' became a capture call; the other three kept their lines, including the one wrapped for Now.
    lines.ShouldBe([1, 5, 6]);
    rewritten.ToString().ShouldContain("[PSLoom.Runtime.Sheds.ShedBridge]::Capture(0)");
    rewritten.ToString().ShouldContain("[PSLoom.Runtime.Sheds.ShedBridge]::Admit(1)");
    rewritten.ToString().ShouldNotContain("Shed -");
  }

  [Fact]
  public void AStatementScriptKeepsTheStatementsLine() {
    var declaration = Declarations("Style 'a:*' 'x' 0\n\nShed -Wait\nStyle 'a:*' 'b' 1").ShouldHaveSingleItem();

    ShedRewriter.StatementScript(declaration, null).Ast.Find(node => node is CommandAst, true)!.Extent.StartLineNumber.ShouldBe(4);
  }

  private static ScriptBlock Rewrite(string body) {
    using var runspace = PowerShellHost.CreateRunspace();
    using var _ = PowerShellHost.UseAsDefault(runspace);
    var draft = ScriptBlock.Create(body);

    return ShedRewriter.Rewrite(draft, Declarations((ScriptBlockAst)draft.Ast));
  }

  private static IReadOnlyList<ShedDeclaration> Declarations(string body)
    => Declarations(Parser.ParseInput(body, out _, out _));

  private static IReadOnlyList<ShedDeclaration> Declarations(ScriptBlockAst ast) {
    var registry = new VerbRegistry();
    registry.Add(typeof(StyleVerb), LoomSession.KERNEL_OWNER);

    return ShedAnalyzer.Analyze(ast, registry).Declarations;
  }
}
