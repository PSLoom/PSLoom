// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Runtime;
using PSLoom.TestKit;

namespace PSLoom.Tests.Runtime;

[TestSubject(typeof(RunspaceLocal<>))]
public sealed class RunspaceLocalTests {
  [Fact]
  public void For_SameRunspace_ReturnsSameValue() {
    using var runspace = PowerShellHost.CreateRunspace();
    var local = new RunspaceLocal<object>(_ => new object());

    local.For(runspace).ShouldBeSameAs(local.For(runspace));
  }

  [Fact]
  public void For_DifferentRunspaces_ReturnsDistinctValues() {
    using var first = PowerShellHost.CreateRunspace();
    using var second = PowerShellHost.CreateRunspace();
    var local = new RunspaceLocal<object>(_ => new object());

    local.For(first).ShouldNotBeSameAs(local.For(second));
  }

  [Fact]
  public void ForCurrent_WithoutDefaultRunspace_ThrowsKernelException() {
    var local = new RunspaceLocal<object>(_ => new object());

    using (PowerShellHost.UseAsDefault(null)) {
      Should.Throw<KernelException>(local.ForCurrent).ErrorId.ShouldBe(KernelException.NO_RUNSPACE);
    }
  }

  [Fact]
  public void TryGet_BeforeFirstAccess_ReturnsFalse() {
    using var runspace = PowerShellHost.CreateRunspace();
    var local = new RunspaceLocal<object>(_ => new object());

    local.TryGet(runspace, out var _).ShouldBeFalse();
    local.For(runspace);
    local.TryGet(runspace, out var value).ShouldBeTrue();
    value.ShouldNotBeNull();
  }
}
