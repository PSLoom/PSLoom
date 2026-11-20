// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Tests;

public sealed class HarnessRegistryTests {
  [Fact]
  public void RegisterReturnsTheContractTheAssemblyWasCompiledAgainst() {
    var info = HarnessRegistry.Register(typeof(HarnessRegistryTests).Assembly);

    Assert.Equal("PSLoom.Tests", info.Name);
    Assert.Equal(ContractVersion.Current, info.CompiledAgainst);
  }

  [Fact]
  public void RegisterIsIdempotent() {
    var first = HarnessRegistry.Register(typeof(HarnessRegistryTests).Assembly);
    var second = HarnessRegistry.Register(typeof(HarnessRegistryTests).Assembly);

    Assert.Same(first, second);
  }

  [Fact]
  public void RegisteredHarnessIsListed() {
    HarnessRegistry.Register(typeof(HarnessRegistryTests).Assembly);

    Assert.Contains(
      HarnessRegistry.GetRegistered(),
      harness => string.Equals(harness.Name, "PSLoom.Tests", StringComparison.Ordinal));
  }

  [Fact]
  public void AssemblyThatDoesNotReferenceTheBaseIsRejected() {
    var exception = Assert.Throws<ArgumentException>(() => HarnessRegistry.Register(typeof(object).Assembly));

    Assert.Contains("does not reference PSLoom", exception.Message, StringComparison.Ordinal);
  }
}
