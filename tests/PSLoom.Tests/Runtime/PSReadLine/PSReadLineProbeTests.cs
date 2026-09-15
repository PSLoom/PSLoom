// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Runtime.PSReadLine;
using PSLoom.Tests.Utility;

namespace PSLoom.Tests.Runtime.PSReadLine;

[TestSubject(typeof(PSReadLineProbe))]
public sealed class PSReadLineProbeTests {
  [Fact]
  public void Probe_WithoutPSReadLine_ReportsNothingAndDoesNotThrow() {
    using var session = new KernelSession();
    var probe = new PSReadLineProbe(session.Engine().InvokeCommand);

    probe.IsLoaded.ShouldBeFalse();
    probe.HasOptionParameter("TokenColorHandler").ShouldBeFalse();
  }

  [Fact]
  public void HasOptionParameter_WithFunctionStandIn_ChecksParameters() {
    using var session = new KernelSession();
    session.Run("function global:Set-PSReadLineOption { param($LineAcceptedHandler) }");
    var probe = new PSReadLineProbe(session.Engine().InvokeCommand);

    probe.IsLoaded.ShouldBeTrue();
    probe.HasOptionParameter("LineAcceptedHandler").ShouldBeTrue();
    probe.HasOptionParameter("TokenColorHandler").ShouldBeFalse();
  }
}
