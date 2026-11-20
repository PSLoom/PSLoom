// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;

namespace PSLoom;

/// <summary>
///   Thrown when a harness was compiled against a contract major that the loaded PSLoom does not provide.
/// </summary>
public sealed class HarnessContractException : InvalidOperationException {
  internal HarnessContractException(string harnessName, Version compiledAgainst, Version loaded)
    : base(
      string.Create(
        CultureInfo.InvariantCulture,
        $"Harness '{harnessName}' was compiled against PSLoom contract {compiledAgainst.Major}.x, but the loaded PSLoom provides contract {loaded.Major}.x. Install a version of '{harnessName}' built for contract {loaded.Major}.x.")) {
    HarnessName = harnessName;
    CompiledAgainst = compiledAgainst;
    Loaded = loaded;
  }

  /// <summary>Gets the name of the harness assembly.</summary>
  public string HarnessName { get; }

  /// <summary>Gets the PSLoom assembly version the harness was compiled against.</summary>
  public Version CompiledAgainst { get; }

  /// <summary>Gets the PSLoom assembly version loaded in the process.</summary>
  public Version Loaded { get; }
}
