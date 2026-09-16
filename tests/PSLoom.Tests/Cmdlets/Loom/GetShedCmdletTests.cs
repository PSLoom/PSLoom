// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Cmdlets.Loom;
using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Sheds;
using PSLoom.Tests.Utility;
using PSLoom.Warp.Dsl;

namespace PSLoom.Tests.Cmdlets.Loom;

[TestSubject(typeof(GetShedCmdlet))]
public sealed class GetShedCmdletTests {
  [Fact]
  public void ListsEveryStagedStatement_AndFiltersByState() {
    using var session = new KernelSession();
    session.Loom.Sheds.IsInteractive = () => true;

    session.Run("Invoke-Loom { Shed -Lucid; Style 'app:*' 'a' 1; Shed -Wait; Style 'app:*' 'b' 2 }");

    var entries = session.Run("Get-Shed").Select(item => (ShedEntry)item.BaseObject).ToArray();
    entries.Select(entry => (entry.Line, entry.State)).ShouldBe([(1, ShedState.Applied), (1, ShedState.Pending)]);
    session.Run("Get-Shed -State Pending").ShouldHaveSingleItem();
  }

  [Fact]
  public void NothingStagedListsNothing() {
    using var session = new KernelSession();

    session.Run("Get-Shed").ShouldBeEmpty();
    session.Streams.Error.ShouldBeEmpty();
  }

  [Fact]
  public void MeasureLoomIncludesCaptureAndDeferredPhases() {
    using var session = new KernelSession();
    session.Loom.Sheds.IsInteractive = () => false;

    session.Run("Invoke-Loom { Shed -Wait; Style 'app:*' 'color' 'Cyan' }");

    var phases = session.Run("Measure-Loom").Select(item => ((LoomTiming)item.BaseObject).Phase).ToArray();
    phases.ShouldContain(LoomPhase.Capture);
    phases.ShouldContain(LoomPhase.Deferred);
  }
}
