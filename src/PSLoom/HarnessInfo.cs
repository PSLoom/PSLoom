// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom;

/// <summary>Describes a harness registered with the base.</summary>
public sealed class HarnessInfo {
  internal HarnessInfo(string name, Version compiledAgainst) {
    Name = name;
    CompiledAgainst = compiledAgainst;
  }

  /// <summary>Gets the name of the harness assembly.</summary>
  public string Name { get; }

  /// <summary>Gets the PSLoom assembly version the harness was compiled against.</summary>
  public Version CompiledAgainst { get; }
}
