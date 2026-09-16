// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Sheds;
using PSLoom.Tests.Utility;
using PSLoom.Warp.Dsl;

namespace PSLoom.Tests.Cmdlets.Loom;

[TestSubject(typeof(ShedBridge))]
public sealed class ShedNowTests {
  [Fact]
  public void AShedWithoutTimingAppliesInPlace_AndSeesDraftVariables() {
    using var session = new KernelSession();

    session.Run("Invoke-Loom { $color = 'Cyan'; Shed -AtLoad { $global:loaded = $true }; Style 'app:*' 'color' $color }");

    session.Streams.Error.ShouldBeEmpty();
    session.Style("app:main", "color").ShouldBe("Cyan");
    session.Global("loaded").ShouldBe(true);
    session.Loom.Sheds.Entries.ShouldHaveSingleItem().State.ShouldBe(ShedState.Applied);
  }

  [Fact]
  public void AFalseConditionSkipsTheStatement() {
    using var session = new KernelSession();

    session.Run("Invoke-Loom { Shed -LoadIf { $false }; Style 'app:*' 'color' 'Cyan' }");

    session.Style("app:main", "color").ShouldBeNull();
    session.Loom.Sheds.Entries.ShouldHaveSingleItem().State.ShouldBe(ShedState.Skipped);
  }

  [Fact]
  public void AMissingCommandSkipsTheStatement() {
    using var session = new KernelSession();

    session.Run("Invoke-Loom { Shed -RequiresCommand 'no-such-command-psloom'; Style 'app:*' 'color' 'Cyan' }");

    session.Style("app:main", "color").ShouldBeNull();
    session.Loom.Sheds.Entries.ShouldHaveSingleItem().State.ShouldBe(ShedState.Skipped);
  }

  [Fact]
  public void LucidDiscardsTheStatementsOutput() {
    using var session = new KernelSession();

    var output = session.Run("Invoke-Loom { Shed -Lucid; 'noisy'; 'kept' }");

    output.Select(item => item.BaseObject).ShouldBe(["kept"]);
  }

  [Fact]
  public void AFailingStatementIsReportedOnce_AndMarkedFailed() {
    using var session = new KernelSession();

    session.Run("Invoke-Loom { Shed -Lucid; Style '' 'color' 'Cyan' }");

    session.Streams.Error.ShouldHaveSingleItem();
    session.Loom.Sheds.Entries.ShouldHaveSingleItem().State.ShouldBe(ShedState.Failed);
  }

  [Fact]
  public void PrepassErrorsBlockTheDraft() {
    using var session = new KernelSession();

    session.Run("Invoke-Loom { Style 'app:*' 'color' 'Cyan'; Shed -Wait -Slot 1a; Style 'app:*' 'size' 1 }");

    session.Streams.Error.ShouldHaveSingleItem().FullyQualifiedErrorId.ShouldStartWith(LoomException.SHED_TIMING_CONFLICT);
    session.Style("app:main", "color").ShouldBeNull();
  }

  [Fact]
  public void LineNumbersSurviveTheRewrite() {
    using var session = new KernelSession();

    session.Run("Invoke-Loom {\n  Shed -Lucid\n  Style 'app:*' 'color' 'Cyan'\n  Style 'app:*' 'size' 1\n}");

    session.Run("Measure-Loom").Select(item => (LoomTiming)item.BaseObject).Where(timing => timing.Phase == LoomPhase.Verb)
      .Select(timing => timing.Line).ShouldBe([3, 4]);
  }
}
