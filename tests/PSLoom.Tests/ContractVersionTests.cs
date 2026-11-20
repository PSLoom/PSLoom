// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Tests;

public sealed class ContractVersionTests {
  [Fact]
  public void SameMajorIsCompatible() {
    var exception = Record.Exception(() =>
      ContractVersion.EnsureCompatible("Sample", new Version(1, 0, 0, 0), new Version(1, 4, 0, 0)));

    Assert.Null(exception);
  }

  [Fact]
  public void DifferentMajorThrowsAndNamesBothVersions() {
    var exception = Assert.Throws<HarnessContractException>(() =>
      ContractVersion.EnsureCompatible("Sample", new Version(1, 0, 0, 0), new Version(2, 0, 0, 0)));

    Assert.Equal("Sample", exception.HarnessName);
    Assert.Equal(new Version(1, 0, 0, 0), exception.CompiledAgainst);
    Assert.Equal(new Version(2, 0, 0, 0), exception.Loaded);
    Assert.Contains("contract 1.x", exception.Message, StringComparison.Ordinal);
    Assert.Contains("contract 2.x", exception.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void AssemblyVersionIsTheContractMajorOnly() {
    Assert.Equal(new Version(0, 0, 0, 0), ContractVersion.Current);
  }
}
