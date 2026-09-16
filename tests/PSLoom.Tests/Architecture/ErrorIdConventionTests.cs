// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime;
using PSLoom.TestKit;
using PSLoom.Warp;

namespace PSLoom.Tests.Architecture;

public sealed class ErrorIdConventionTests {
  [Fact]
  public void WarpIdsUseTheContractPrefixes()
    => ErrorIdConvention.Violations(typeof(WarpException).Assembly, "WARP", "CREEL").ShouldBeEmpty();

  [Fact]
  public void KernelIdsUseTheKernelPrefixes()
    => ErrorIdConvention.Violations(typeof(KernelException).Assembly, "LOOM", "STYLE", "HOOK", "TREADLE").ShouldBeEmpty();

  [Fact]
  public void TheConventionFindsTheIds() {
    // Guards the test itself: a reflection change that found nothing would make the checks above pass vacuously.
    ErrorIdConvention.Ids(typeof(WarpException).Assembly).ShouldContain(entry => entry.Id == "WARP_SCOPE_INVALID");
    ErrorIdConvention.Ids(typeof(KernelException).Assembly).Count.ShouldBeGreaterThan(30);
  }
}
