// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Language;
using JetBrains.Annotations;
using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Sheds;
using PSLoom.Runtime.Verbs;
using PSLoom.Verbs;

namespace PSLoom.Tests.Runtime.Sheds;

[TestSubject(typeof(ShedAnalyzer))]
public sealed class ShedAnalyzerTests {
  [Fact]
  public void ADraftWithoutShedHasNoDeclarations() {
    var (declarations, errors) = Analyze("Style 'a:*' 'b' 1");

    declarations.ShouldBeEmpty();
    errors.ShouldBeEmpty();
  }

  [Fact]
  public void WaitStagesTheNextStatementInSlotZeroA() {
    var (declarations, errors) = Analyze("Shed -Wait\nStyle 'a:*' 'b' 1");

    errors.ShouldBeEmpty();
    var declaration = declarations.ShouldHaveSingleItem();
    declaration.Index.ShouldBe(0);
    declaration.Timing.ShouldBe(ShedTiming.Wait);
    declaration.Slot.ShouldBe("0a");
    declaration.IsVerb.ShouldBeTrue();
    declaration.Statement.Extent.Text.ShouldBe("Style 'a:*' 'b' 1");
  }

  [Fact]
  public void EveryModifierIsRead() {
    var (declarations, errors) = Analyze("Shed -Slot 1b -LoadIf { $true } -RequiresCommand git -Lucid -Silent -AtLoad { 1 }\nImport-Module foo");

    errors.ShouldBeEmpty();
    var declaration = declarations.ShouldHaveSingleItem();
    declaration.Timing.ShouldBe(ShedTiming.Slot);
    declaration.Slot.ShouldBe("1b");
    declaration.LoadIf!.ToString().Trim().ShouldBe("$true");
    declaration.RequiresCommand.ShouldBe("git");
    declaration.Lucid.ShouldBeTrue();
    declaration.Silent.ShouldBeTrue();
    declaration.AtLoad!.ToString().Trim().ShouldBe("1");
    declaration.IsVerb.ShouldBeFalse();
  }

  [Fact]
  public void AShedWithoutTimingAppliesNow() {
    var declaration = Analyze("Shed -Lucid\nStyle 'a:*' 'b' 1").Declarations.ShouldHaveSingleItem();

    declaration.Timing.ShouldBe(ShedTiming.Now);
  }

  [Theory]
  [InlineData("Style 'a:*' 'b' 1\nShed -Wait", LoomException.SHED_DANGLING)]
  [InlineData("Shed -Wait\nShed -Lucid\nStyle 'a:*' 'b' 1", LoomException.SHED_STACKED)]
  [InlineData("if ($true) { Shed -Wait }\nStyle 'a:*' 'b' 1", LoomException.SHED_NOT_TOP_LEVEL)]
  [InlineData("Shed -Slot $slot\nStyle 'a:*' 'b' 1", LoomException.SHED_NOT_LITERAL)]
  [InlineData("Shed -Slot 'x9'\nStyle 'a:*' 'b' 1", LoomException.SHED_NOT_LITERAL)]
  [InlineData("Shed -RequiresCommand $name\nStyle 'a:*' 'b' 1", LoomException.SHED_NOT_LITERAL)]
  [InlineData("Shed -LoadIf $condition\nStyle 'a:*' 'b' 1", LoomException.SHED_NOT_LITERAL)]
  [InlineData("Shed -Wait -Slot 1a\nStyle 'a:*' 'b' 1", LoomException.SHED_TIMING_CONFLICT)]
  [InlineData("Shed -OnDemand\nStyle 'a:*' 'b' 1", LoomException.SHED_NOT_ON_DEMAND)]
  [InlineData("Shed -Wait\nThread Fixture", LoomException.SHED_NOT_APPLICABLE)]
  [InlineData("Shed -Soon\nStyle 'a:*' 'b' 1", LoomException.SHED_UNKNOWN_MODIFIER)]
  public void RuleViolationsAreReported(string draft, string errorId)
    => Analyze(draft).Errors.ShouldContain(error => error.ErrorId == errorId);

  [Fact]
  public void DeclarationsAreIndexedInDraftOrder() {
    var (declarations, _) = Analyze("Shed -Wait\nStyle 'a:*' 'b' 1\nStyle 'a:*' 'c' 2\nShed -Slot 1a\nStyle 'a:*' 'd' 3");

    declarations.Select(declaration => declaration.Index).ShouldBe([0, 1]);
    declarations[1].Statement.Extent.StartLineNumber.ShouldBe(5);
  }

  private static (IReadOnlyList<ShedDeclaration> Declarations, IReadOnlyList<LoomException> Errors) Analyze(string draft) {
    var registry = new VerbRegistry();
    registry.Add(typeof(StyleVerb), LoomSession.KERNEL_OWNER);
    registry.Add(typeof(ThreadVerb), LoomSession.KERNEL_OWNER);

    return ShedAnalyzer.Analyze(Parser.ParseInput(draft, out _, out _), registry);
  }
}
