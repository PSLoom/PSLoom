// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Runtime.Hooks;
using PSLoom.Runtime.Sheds;
using PSLoom.Tests.Utility;
using PSLoom.Warp.Dsl;
using PSLoom.Warp.Hooks;

namespace PSLoom.Tests.Cmdlets.Loom;

[TestSubject(typeof(ShedStaging))]
public sealed class ShedInteractiveTests {
  [Fact]
  public void TheFirstPromptAppliesTheQueue() {
    using var session = Interactive();

    session.Run("Invoke-Loom { Shed -Wait; Style 'app:*' 'color' 'Cyan' }");
    session.Style("app:main", "color").ShouldBeNull();

    session.Run("prompt | Out-Null");

    session.Style("app:main", "color").ShouldBe("Cyan");
    session.Loom.Sheds.Entries.ShouldHaveSingleItem().State.ShouldBe(ShedState.Applied);
  }

  [Fact]
  public void AnIdleTickAppliesTheQueueToo() {
    using var session = Interactive();

    session.Run("Invoke-Loom { Shed -Wait; Style 'app:*' 'color' 'Cyan' }");
    session.Run("New-Event -SourceIdentifier ([System.Management.Automation.PSEngineEvent]::OnIdle) | Out-Null");

    session.Style("app:main", "color").ShouldBe("Cyan");
  }

  [Fact]
  public void AFiringStopsAtTheSlice() {
    using var session = Interactive();
    long now = 0;
    session.Loom.Sheds.Timestamp = () => now += System.Diagnostics.Stopwatch.Frequency / 50; // every read advances 20 ms

    session.Run("Invoke-Loom { Shed -Wait; Style 'app:*' 'a' 1; Shed -Wait; Style 'app:*' 'b' 2 }");
    session.Run("prompt | Out-Null");

    session.Style("app:main", "a").ShouldBe(1);
    session.Style("app:main", "b").ShouldBeNull();
    session.Loom.Sheds.Queue.Count.ShouldBe(1);
  }

  [Fact]
  public void ACommandTypedBeforeTheQueueDrainsIsRescued() {
    using var session = Interactive();

    session.Run("Invoke-Loom { Shed -Slot 3a; function global:staged-late { 'rescued' } }");

    session.Run("staged-late").Single().BaseObject.ShouldBe("rescued");
    session.Loom.Sheds.Queue.Count.ShouldBe(0);
  }

  [Fact]
  public void TheInternalHandlersAreNotListedByGetHook() {
    using var session = Interactive();

    session.Run("Invoke-Loom { Shed -Wait; Style 'app:*' 'color' 'Cyan' }");

    session.Run("Get-Hook").ShouldBeEmpty();
  }

  [Fact]
  public void ADraftWithoutStagedStatementsWiresNothing() {
    using var session = Interactive();

    session.Run("Invoke-Loom { Style 'app:*' 'color' 'Cyan' }");

    HookBus.PerRunspace.For(session.Runspace).Wiring.IsWired(HookKind.CommandNotFound).ShouldBeFalse();
  }

  [Fact]
  public void AFailureIsWarnedAboutOnceAtThePrompt() {
    using var session = Interactive();

    session.Run("Invoke-Loom { Shed -Wait; Style '' 'color' 'Cyan'; Shed -Wait -Silent; Style '' 'size' 1 }");
    session.Run("prompt | Out-Null");
    session.Run("prompt | Out-Null");

    // One warning line for the non-silent failure, not repeated on the second prompt.
    session.Streams.Warning.ShouldHaveSingleItem().Message.ShouldContain("1 staged statement failed");
  }

  private static KernelSession Interactive() {
    var session = new KernelSession();
    session.Loom.Sheds.IsInteractive = () => true;

    return session;
  }
}
