// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Warp.Hosting;

namespace PSLoom.Warp.Tests.Hosting;

[TestSubject(typeof(WarpContract))]
public sealed class WarpContractTests {
  [Fact]
  public void CompiledVersionIsReadFromTheAssemblyReference()
    => WarpContract.GetCompiledVersion(typeof(WarpContractTests).Assembly).ShouldBe(WarpContract.Version);

  [Fact]
  public void AssemblyWithoutWarpReferenceHasNoCompiledVersion() => WarpContract.GetCompiledVersion(typeof(object).Assembly).ShouldBeNull();

  [Theory]
  [InlineData(1, 0, true)]
  [InlineData(1, 7, true)]
  [InlineData(2, 0, false)]
  [InlineData(0, 9, false)]
  public void OnlyTheMajorVersionDecidesCompatibility(int major, int minor, bool expected) {
    WarpContract.Version.Major.ShouldBe(1);

    WarpContract.IsCompatible(new Version(major, minor, 0, 0)).ShouldBe(expected);
  }

  [Fact]
  public void MissingReferenceIsIncompatible()
    => WarpContract.IsCompatible(null).ShouldBeFalse();
}
