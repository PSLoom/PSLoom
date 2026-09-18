// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Runtime.Sheds;
using PSLoom.TestKit;
using PSLoom.Tests.Utility;
using PSLoom.Warp.Dsl;

namespace PSLoom.Tests.Cmdlets.Loom;

[TestSubject(typeof(ShedStaging))]
public sealed class ShedInvariantTests {
  [Fact]
  public void StagedStatementsNeverApplyOutOfSlotOrder_EvenAcrossFirings() {
    using var session = new KernelSession();
    session.Loom.Sheds.IsInteractive = () => true;
    session.Run("$global:order = @()");
    long now = 0;
    session.Loom.Sheds.Timestamp = () => now += System.Diagnostics.Stopwatch.Frequency; // one entry per firing

    session.Run(
      """
      Invoke-Loom {
        Shed -Slot 2a; Set-Variable order (@($global:order) + '2a') -Scope Global
        Shed -Slot 0b; Set-Variable order (@($global:order) + '0b') -Scope Global
        Shed -Wait;    Set-Variable order (@($global:order) + '0a') -Scope Global
        Shed -Slot 1c; Set-Variable order (@($global:order) + '1c') -Scope Global
      }
      """);

    for (var firing = 0; firing < 4; firing++) {
      session.Run(firing % 2 == 0
        ? "prompt | Out-Null"
        : "New-Event -SourceIdentifier ([System.Management.Automation.PSEngineEvent]::OnIdle) | Out-Null");
    }

    ((object[])session.Global("order")!).ShouldBe(["0a", "0b", "1c", "2a"]);
  }

  [Fact]
  public void ApplyingFromAPromptAnIdleTickOrAMissedCommandNeverThrows() {
    using var session = new KernelSession();
    session.Loom.Sheds.IsInteractive = () => true;
    session.Run("Invoke-Loom { Shed -Wait; throw 'staged boom'; Shed -Wait; throw 'idle boom'; Shed -Slot 5a; throw 'rescue boom' }");
    var staging = session.Loom.Sheds;

    using (PowerShellHost.UseAsDefault(session.Runspace)) {
      long now = 0;
      staging.Timestamp = () => now += System.Diagnostics.Stopwatch.Frequency; // every read spends a second: one entry per call
      Should.NotThrow(staging.OnPrompt);
      Should.NotThrow(staging.OnIdle);
    }

    Should.NotThrow(() => session.Run("no-such-command-after-staging"));

    staging.Entries.ShouldAllBe(entry => entry.State == ShedState.Failed);
  }
}
