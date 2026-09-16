// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Sheds;
using PSLoom.Tests.Utility;
using PSLoom.Warp.Dsl;

namespace PSLoom.Tests.Cmdlets.Loom;

[TestSubject(typeof(ShedApplier))]
public sealed class ShedDeferredTests {
  [Fact]
  public void ANonInteractiveHostAppliesStagedStatementsAtTheEndOfTheDraft_InSlotOrder() {
    using var session = new KernelSession();
    session.Loom.Sheds.IsInteractive = () => false;
    session.Run("$global:order = @()");

    session.Run(
      """
      Invoke-Loom {
        Shed -Slot 1a
        Set-Variable -Name order -Value (@($global:order) + 'slot-1a') -Scope Global
        Shed -Wait
        Set-Variable -Name order -Value (@($global:order) + 'wait') -Scope Global
        Set-Variable -Name order -Value (@($global:order) + 'eager') -Scope Global
      }
      """);

    session.Streams.Error.ShouldBeEmpty();
    ((object[])session.Global("order")!).ShouldBe(["eager", "wait", "slot-1a"]);
    session.Loom.Sheds.Entries.ShouldAllBe(entry => entry.State == ShedState.Applied);
  }

  [Fact]
  public void AStagedStatementThatIsNotAVerbRunsInTheGlobalScope() {
    using var session = new KernelSession();
    session.Loom.Sheds.IsInteractive = () => false;

    session.Run("Invoke-Loom { Shed -Wait; function staged-function { 'ok' } }");

    session.Run("staged-function").Single().BaseObject.ShouldBe("ok");
  }

  [Fact]
  public void AStagedVerbAppliesWithItsHarnessVocabulary() {
    using var session = new KernelSession();
    session.Loom.Sheds.IsInteractive = () => false;

    session.Run("Invoke-Loom { Thread Fixture; Shed -Wait; Box tools { Item hammer } }");

    session.Streams.Error.ShouldBeEmpty();
    session.Style("fixture:box:tools", "items").ShouldBe("hammer");
  }

  [Fact]
  public void AnInteractiveHostLeavesThemPending() {
    using var session = new KernelSession();
    session.Loom.Sheds.IsInteractive = () => true;

    session.Run("Invoke-Loom { Shed -Wait; Style 'app:*' 'color' 'Cyan' }");

    session.Style("app:main", "color").ShouldBeNull();
    session.Loom.Sheds.Entries.ShouldHaveSingleItem().State.ShouldBe(ShedState.Pending);
    session.Loom.Sheds.Queue.Count.ShouldBe(1);
  }

  [Fact]
  public void AFailureAtTheEndOfANonInteractiveDraftIsWrittenAsAnError() {
    using var session = new KernelSession();
    session.Loom.Sheds.IsInteractive = () => false;

    session.Run("Invoke-Loom { Shed -Wait; Style '' 'color' 'Cyan' }");

    session.Streams.Error.ShouldNotBeEmpty();
    session.Loom.Sheds.Entries.ShouldHaveSingleItem().State.ShouldBe(ShedState.Failed);
  }

  [Fact]
  public void DeferredApplyIsMeasured() {
    using var session = new KernelSession();
    session.Loom.Sheds.IsInteractive = () => false;

    session.Run("Invoke-Loom { Shed -Wait; Style 'app:*' 'color' 'Cyan' }");

    session.Loom.Sheds.Timings.ShouldHaveSingleItem().Phase.ShouldBe(LoomPhase.Deferred);
  }
}
