// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Runtime.Sheds;
using PSLoom.Tests.Utility;
using PSLoom.Warp.Dsl;

namespace PSLoom.Tests.Cmdlets.Loom;

[TestSubject(typeof(ShedStaging))]
public sealed class ShedReweaveTests {
  private const string STAGED = "Shed -Wait; Style 'app:*' 'color' 'Cyan'";

  [Fact]
  public void AnUnchangedPendingStatementStaysQueuedOnce() {
    using var session = Interactive();
    session.Run($"Invoke-Loom {{ {STAGED} }}");

    session.Run($"Invoke-Loom -Reweave {{ {STAGED} }}");

    session.Loom.Sheds.Queue.Count.ShouldBe(1);
  }

  [Fact]
  public void AChangedPendingStatementReplacesTheQueuedOne() {
    using var session = Interactive();
    session.Run($"Invoke-Loom {{ {STAGED} }}");

    session.Run("Invoke-Loom -Reweave { Shed -Wait; Style 'app:*' 'color' 'Red' }");
    session.Run("prompt | Out-Null");

    session.Loom.Sheds.Queue.Count.ShouldBe(0);
    session.Style("app:main", "color").ShouldBe("Red");
  }

  [Fact]
  public void ARemovedPendingStatementLeavesTheQueue() {
    using var session = Interactive();
    session.Run($"Invoke-Loom {{ {STAGED} }}");

    session.Run("Invoke-Loom -Reweave { Style 'app:*' 'size' 1 }");

    session.Loom.Sheds.Queue.Count.ShouldBe(0);
    session.Run("prompt | Out-Null");
    session.Style("app:main", "color").ShouldBeNull();
  }

  [Fact]
  public void ARemovedAppliedStatementIsReverted() {
    using var session = Interactive();
    session.Run($"Invoke-Loom {{ {STAGED} }}");
    session.Run("prompt | Out-Null");
    session.Style("app:main", "color").ShouldBe("Cyan");

    session.Run("Invoke-Loom -Reweave { Style 'app:*' 'size' 1 }");

    session.Style("app:main", "color").ShouldBeNull();
  }

  [Fact]
  public void AnAppliedStatementNowEagerIsNotRevertedByItsOwnRemoval() {
    using var session = Interactive();
    session.Run($"Invoke-Loom {{ {STAGED} }}");
    session.Run("prompt | Out-Null");

    session.Run("Invoke-Loom -Reweave { Style 'app:*' 'color' 'Cyan' }");

    session.Style("app:main", "color").ShouldBe("Cyan");
  }

  [Fact]
  public void AnUnchangedApplyNowStatementIsNotRevertedOnTheNextReweave() {
    using var session = Interactive();
    const string NOW = "Shed -Lucid; Style 'app:*' 'color' 'Cyan'";
    session.Run($"Invoke-Loom {{ {NOW} }}");

    session.Run($"Invoke-Loom -Reweave {{ {NOW} }}");
    session.Run($"Invoke-Loom -Reweave {{ {NOW} }}");

    session.Style("app:main", "color").ShouldBe("Cyan");
    session.Loom.Sheds.Entries.ShouldHaveSingleItem().State.ShouldBe(ShedState.Applied);
  }

  [Fact]
  public void AFailedStatementIsRetried() {
    using var session = Interactive();
    session.Run("Invoke-Loom { Shed -Wait; Style $global:context 'color' 'Cyan' }");
    session.Run("prompt | Out-Null");
    session.Loom.Sheds.Entries.ShouldHaveSingleItem().State.ShouldBe(ShedState.Failed);

    session.Run("$global:context = 'app:*'");
    session.Run("Invoke-Loom -Reweave { Shed -Wait; Style $global:context 'color' 'Cyan' }");
    session.Run("prompt | Out-Null");

    session.Style("app:main", "color").ShouldBe("Cyan");
  }

  private static KernelSession Interactive() {
    var session = new KernelSession();
    session.Loom.Sheds.IsInteractive = () => true;

    return session;
  }
}
