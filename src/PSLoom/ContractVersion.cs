// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom;

internal static class ContractVersion {
  internal static Version Current { get; } = typeof(ContractVersion).Assembly.GetName().Version
                                             ?? throw new InvalidOperationException(
                                               "The PSLoom assembly has no version.");

  internal static void EnsureCompatible(string harnessName, Version compiledAgainst, Version current) {
    if (compiledAgainst.Major != current.Major) {
      throw new HarnessContractException(harnessName, compiledAgainst, current);
    }
  }
}
