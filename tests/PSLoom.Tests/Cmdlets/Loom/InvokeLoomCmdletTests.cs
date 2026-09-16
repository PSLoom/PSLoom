// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Cmdlets.Loom;
using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Styles;
using PSLoom.Tests.Utility;
using PSLoom.Warp;

namespace PSLoom.Tests.Cmdlets.Loom;

[TestSubject(typeof(InvokeLoomCmdlet))]
[TestSubject(typeof(MeasureLoomCmdlet))]
public sealed class InvokeLoomCmdletTests {
  [Fact]
  public void Draft_ThreadsFixtureAndWeavesItsVerbs() {
    using var session = new KernelSession();

    session.Run(
      """
      Invoke-Loom {
        Style 'app:*' 'color' 'Cyan'
        Thread Fixture
        Box tools {
          Item hammer
          Item -N saw -Loud
        }
      }
      """);

    session.Streams.Error.ShouldBeEmpty();
    session.Style("app:main", "color").ShouldBe("Cyan");
    session.Style("fixture:box:tools", "items").ShouldBe("hammer,SAW");
    session.Style("fixture:identity", "module").ShouldBe("PSLoom.Fixture");
    session.Loom.IsWoven.ShouldBeTrue();
  }

  [Fact]
  public void Draft_OutputIsWrittenThrough_AndVerbsDoNotLeak() {
    using var session = new KernelSession();

    var output = session.Run("Invoke-Loom { 'hello'; Style 'a:*' 'b' 1 }; [bool](Get-Command Style -ErrorAction SilentlyContinue)");

    output.Select(item => item.BaseObject).ShouldBe(["hello", false]);
  }

  [Fact]
  public void SecondInvocation_IsASilentNoOp() {
    using var session = new KernelSession();
    session.Run("Invoke-Loom { Style 'a:*' 'b' 'first' }");

    session.Run("Invoke-Loom { Style 'a:*' 'b' 'second' } -Verbose");

    session.Streams.Error.ShouldBeEmpty();
    session.Streams.Verbose.ShouldContain(record => record.Message.Contains("already woven"));
    session.Style("a:x", "b").ShouldBe("first");
  }

  [Fact]
  public void FailingVerb_DoesNotAbortTheDraft_AndIsReportedOnce() {
    using var session = new KernelSession();

    session.Run(
      """
      Invoke-Loom {
        Thread Fixture
        Fail 'first failure'
        Box b { Item one; Fail 'inside box'; Item two }
        Style 'after:*' 'ran' $true
      }
      """);

    session.Style("after:x", "ran").ShouldBe(true);
    session.Style("fixture:box:b", "items").ShouldBe("one,two");
    session.Streams.Error.Select(error => error.Exception.Message).ShouldBe(["first failure", "inside box"]);
  }

  [Fact]
  public void TerminatingStatement_EndsTheDraftAndIsReportedOnce() {
    using var session = new KernelSession();

    session.Run("Invoke-Loom { Style 'a:*' 'before' 1; throw 'draft stopped'; Style 'a:*' 'after' 1 }");

    session.Style("a:x", "before").ShouldBe(1);
    session.Style("a:x", "after").ShouldBeNull();
    session.Streams.Error.ShouldHaveSingleItem().Exception.Message.ShouldBe("draft stopped");
    session.Loom.IsWoven.ShouldBeTrue();
  }

  [Fact]
  public void OutOfScopeVerb_BlocksExecution() {
    using var session = new KernelSession();

    session.Run("Invoke-Loom {\n  Style 'a:*' 'b' 1\n  Thread Fixture\n  Item stray\n}");

    var error = session.Streams.Error.ShouldHaveSingleItem();
    error.FullyQualifiedErrorId.ShouldStartWith(LoomException.VERB_OUT_OF_SCOPE);
    error.Exception.Message.ShouldContain("line 4");
    session.Style("a:x", "b").ShouldBeNull();
    session.Loom.IsWoven.ShouldBeFalse();
  }

  [Fact]
  public void DynamicallyNamedVerbOutOfScope_IsCaughtAtRunTime_AndSkipped() {
    using var session = new KernelSession();

    session.Run("Invoke-Loom { Thread Fixture; Box b { Item one; & ('Sty' + 'le') 'a:*' 'b' 1; Item two } }");

    session.Streams.Error.ShouldHaveSingleItem().FullyQualifiedErrorId.ShouldStartWith(LoomException.VERB_OUT_OF_SCOPE);
    session.Style("a:x", "b").ShouldBeNull();
    session.Style("fixture:box:b", "items").ShouldBe("one,two");
  }

  [Fact]
  public void Validate_ImportsAndChecksButNeverExecutes() {
    using var session = new KernelSession();

    session.Run("Invoke-Loom -Validate { Thread Fixture; Box b { Item x } } -Verbose");

    session.Streams.Error.ShouldBeEmpty();
    session.Streams.Verbose.ShouldContain(record => record.Message.Contains("valid"));
    session.Style("fixture:box:b", "items").ShouldBeNull();
    session.Loom.Harnesses.TryGetByName("Fixture", out var _).ShouldBeTrue();
    session.Loom.IsWoven.ShouldBeFalse();
  }

  [Fact]
  public void ThreadErrors_AreReportedTogetherAndBlockExecution() {
    using var session = new KernelSession();

    session.Run("Invoke-Loom { if ($true) { Thread Fixture }; Thread $name; Style 'a:*' 'b' 1 }");

    session.Streams.Error.Select(error => ((PowerShellException)error.Exception).ErrorId)
      .ShouldBe([LoomException.THREAD_NOT_TOP_LEVEL, LoomException.THREAD_NAME_NOT_LITERAL], true);
    session.Style("a:x", "b").ShouldBeNull();
  }

  [Fact]
  public void MissingHarnessModule_UnderValidate_ReportsInstallCommandWithoutInstalling() {
    using var session = new KernelSession();
    session.Loom.FirstParty.Add("DoesNotExist12345");

    session.Run("Invoke-Loom -Validate { Thread DoesNotExist12345 }");

    var error = session.Streams.Error.ShouldHaveSingleItem();
    error.FullyQualifiedErrorId.ShouldStartWith(LoomException.HARNESS_NOT_INSTALLED);
    error.Exception.Message.ShouldContain("Install-PSResource -Name PSLoom.DoesNotExist12345");
  }

  [Fact]
  public void ModuleWithoutThatHarness_IsNotAHarness() {
    using var session = new KernelSession();

    // PSLoom.Stub (tests/PSLoom.Tests/TestModules) is a manifest-only module that registers no harness.
    session.Loom.FirstParty.Add("Stub");
    session.Run("Invoke-Loom { Thread Stub }");

    var error = session.Streams.Error.ShouldHaveSingleItem();
    error.FullyQualifiedErrorId.ShouldStartWith(LoomException.NOT_A_HARNESS, customMessage: error.Exception.ToString());
  }

  [Fact]
  public void StyleVerb_WatcherFailure_IsReportedWithoutStoppingTheDraft() {
    using var session = new KernelSession();
    session.Run("Register-StyleWatcher 'app:main' 'color' { throw 'watcher boom' } | Out-Null");

    session.Run("Invoke-Loom { Style 'app:*' 'color' 'Cyan'; Style 'app:*' 'size' 12 }");

    session.Style("app:main", "size").ShouldBe(12);
    session.Streams.Error.ShouldHaveSingleItem().FullyQualifiedErrorId.ShouldStartWith(StyleException.WATCHER_FAILED);
  }

  [Fact]
  public void SessionStarting_IsRaisedAtTheEndOfTheDraft_AndNotAgainAtTheFirstPrompt() {
    using var session = new KernelSession();

    session.Run("Invoke-Loom { Thread Fixture }");
    session.Style("fixture:events", "session-starting").ShouldBe(1);

    session.Run("Register-Hook PrePrompt { } | Out-Null; prompt | Out-Null; prompt | Out-Null");

    session.Style("fixture:events", "session-starting").ShouldBe(1);
  }

  [Fact]
  public void MeasureLoom_ReportsPhasesAndVerbs_WithGrouping() {
    using var session = new KernelSession();
    session.Run("Measure-Loom");
    session.Streams.Warning.ShouldNotBeEmpty();

    session.Run("Invoke-Loom {\nThread Fixture\nBox b {\n  Item one\n  Item two\n}\nStyle 'a:*' 'b' 1\n}");

    var rows = session.Run("Measure-Loom").Select(row => row.BaseObject).Cast<LoomTiming>().ToArray();
    rows.Select(row => row.Phase).Distinct()
      .ShouldBe([LoomPhase.Prepass, LoomPhase.Import, LoomPhase.Validate, LoomPhase.Verb, LoomPhase.Total], true);
    var box = rows.Single(row => row is { Phase: LoomPhase.Verb, Name: "Box" });
    box.Line.ShouldBe(3);
    box.Depth.ShouldBe(0);
    box.Harness.ShouldBe("Fixture");
    rows.Where(row => row.Name == "Item").ShouldAllBe(row => row.Depth == 1);
    box.Exclusive.ShouldBeLessThanOrEqualTo(box.Inclusive);

    var byHarness = session.Run("Measure-Loom -GroupBy Harness").Select(row => row.BaseObject).Cast<LoomTiming>().ToArray();
    byHarness.Single(row => row.Name == "Fixture").Count.ShouldBe(3);
    byHarness.Single(row => row.Name == LoomSession.KERNEL_OWNER).Count.ShouldBe(2);

    session.Run("Measure-Loom -GroupBy Verb").Select(row => row.BaseObject).Cast<LoomTiming>().Single(row => row.Name == "Item").Count.ShouldBe(2);
  }
}
