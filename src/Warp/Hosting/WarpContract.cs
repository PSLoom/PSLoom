// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Reflection;

namespace PSLoom.Warp.Hosting;

/// <summary>
///   The version of the contract loaded in this process, and the compatibility rule harnesses are held to.
/// </summary>
public static class WarpContract {
  private const string ASSEMBLY_NAME = "Warp";

  /// <summary>
  ///   Gets the loaded contract version. Only <see cref="Version.Major" /> is meaningful; additive releases keep it.
  /// </summary>
  public static Version Version { get; } = typeof(WarpContract).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);

  /// <summary>
  ///   Gets the Warp version an assembly was compiled against, read from its assembly reference rather than declared.
  /// </summary>
  /// <param name="assembly">The harness assembly.</param>
  /// <returns>The referenced Warp version, or <see langword="null" /> when the assembly does not reference Warp.</returns>
  public static Version? GetCompiledVersion(Assembly assembly) {
    ArgumentNullException.ThrowIfNull(assembly);

    foreach (var reference in assembly.GetReferencedAssemblies()) {
      if (string.Equals(reference.Name, ASSEMBLY_NAME, StringComparison.Ordinal)) {
        return reference.Version;
      }
    }

    return null;
  }

  /// <summary>
  ///   Checks whether an assembly compiled against <paramref name="compiled" /> can run against the loaded contract.
  /// </summary>
  /// <param name="compiled">The compiled-against version.</param>
  /// <returns><see langword="true" /> when the major versions match.</returns>
  public static bool IsCompatible(Version? compiled)
    => compiled is not null && compiled.Major == Version.Major;

  internal static Version EnsureCompatible(Assembly assembly) {
    var compiled = GetCompiledVersion(assembly);

    return IsCompatible(compiled)
      ? compiled!
      : throw WarpException.ContractMismatch(assembly.GetName().Name ?? assembly.FullName ?? "<unknown>", compiled, Version);
  }
}
