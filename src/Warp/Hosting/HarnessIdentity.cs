// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Hosting;

/// <summary>
///   Who a harness is. Built by the kernel; every member is get-only.
/// </summary>
public sealed class HarnessIdentity {
  internal HarnessIdentity(string name, string? description, string moduleName, Version moduleVersion, Version compiledWarpVersion) {
    Name = name;
    Description = description;
    ModuleName = moduleName;
    ModuleVersion = moduleVersion;
    CompiledWarpVersion = compiledWarpVersion;
  }

  /// <summary>
  ///   Gets the harness name from <see cref="HarnessAttribute" />.
  /// </summary>
  public string Name { get; }

  /// <summary>
  ///   Gets the description from <see cref="HarnessAttribute" />.
  /// </summary>
  public string? Description { get; }

  /// <summary>
  ///   Gets the name of the module that registered the harness.
  /// </summary>
  public string ModuleName { get; }

  /// <summary>
  ///   Gets the version of the module that registered the harness.
  /// </summary>
  public Version ModuleVersion { get; }

  /// <summary>
  ///   Gets the Warp version the harness assembly was compiled against, read from its assembly reference.
  /// </summary>
  public Version CompiledWarpVersion { get; }

  /// <inheritdoc />
  public override string ToString()
    => $"{Name} ({ModuleName} {ModuleVersion})";
}
