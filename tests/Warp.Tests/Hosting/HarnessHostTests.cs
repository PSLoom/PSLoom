// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation;
using JetBrains.Annotations;
using PSLoom.Warp.Hosting;

namespace PSLoom.Warp.Tests.Hosting;

[TestSubject(typeof(HarnessHost))]
public sealed class HarnessHostTests {
  [Fact]
  public void RegisterWithoutAttributeFailsBeforeReachingTheKernel() {
    var exception = Should.Throw<WarpException>(HarnessHost.Register<UnmarkedHarness>);

    exception.ErrorId.ShouldBe(WarpException.HARNESS_ATTRIBUTE_MISSING);
  }

  [Fact]
  public void RegisterWithoutKernelFailsClearly() {
    var exception = Should.Throw<WarpException>(HarnessHost.Register<MarkedHarness>);

    exception.ErrorId.ShouldBe(WarpException.KERNEL_UNAVAILABLE);
  }

  [Fact]
  public void ErrorRecordCarriesErrorIdAndCategory() {
    var record = Should.Throw<WarpException>(HarnessHost.Register<UnmarkedHarness>).ToErrorRecord();

    record.FullyQualifiedErrorId.ShouldBe(WarpException.HARNESS_ATTRIBUTE_MISSING);
    record.CategoryInfo.Category.ShouldBe(ErrorCategory.InvalidData);
    record.TargetObject.ShouldBe(typeof(UnmarkedHarness));
  }

  private sealed class UnmarkedHarness : IHarness {
    public void Compose(IHarnessBuilder builder) { }
  }

  [Harness("Marked")]
  private sealed class MarkedHarness : IHarness {
    public void Compose(IHarnessBuilder builder) { }
  }
}
