// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Runspaces;
using PSLoom.Cmdlets;
using PSLoom.TestKit;

namespace PSLoom.Tests.Cmdlets;

public sealed class GetHarnessCmdletTests {
  [Fact]
  public void GetHarnessEmitsTheRegisteredHarnesses() {
    HarnessRegistry.Register(typeof(GetHarnessCmdletTests).Assembly);

    using var runspace = PowerShellHost.CreateRunspace(state
      => state.Commands.Add(new SessionStateCmdletEntry("Get-Harness", typeof(GetHarnessCmdlet), null)));
    using var shell = PowerShellHost.CreateShell(runspace);

    var harnesses = shell.AddCommand("Get-Harness").Invoke<HarnessInfo>();

    Assert.False(shell.HadErrors);
    Assert.Contains(harnesses, harness => string.Equals(harness.Name, "PSLoom.Tests", StringComparison.Ordinal));
  }
}
