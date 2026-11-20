// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Collections.Concurrent;
using System.Reflection;

namespace PSLoom;

/// <summary>
///   The process-wide list of harnesses. A harness registers itself when its module is imported, and the base
///   refuses one that was compiled against another contract major.
/// </summary>
public static class HarnessRegistry {
  private const string BASE_ASSEMBLY_NAME = "PSLoom";

  private readonly static ConcurrentDictionary<string, HarnessInfo> Harnesses = new(StringComparer.OrdinalIgnoreCase);

  /// <summary>Registers a harness assembly after checking the contract it was compiled against.</summary>
  /// <param name="harness">The assembly of the harness module.</param>
  /// <returns>The registration; the same instance when the harness was already registered.</returns>
  /// <exception cref="ArgumentException">The assembly does not reference PSLoom.</exception>
  /// <exception cref="HarnessContractException">The harness targets another contract major.</exception>
  public static HarnessInfo Register(Assembly harness) {
    ArgumentNullException.ThrowIfNull(harness);

    var name = harness.GetName().Name ??
               throw new ArgumentException("The harness assembly has no name.", nameof(harness));
    var reference = Array.Find(harness.GetReferencedAssemblies(),
      candidate => string.Equals(candidate.Name, BASE_ASSEMBLY_NAME, StringComparison.Ordinal));

    if (reference?.Version is not { } compiledAgainst) {
      throw new ArgumentException($"Assembly '{name}' does not reference PSLoom, so it is not a harness.",
        nameof(harness));
    }

    ContractVersion.EnsureCompatible(name, compiledAgainst, ContractVersion.Current);

    return Harnesses.GetOrAdd(name, static (key, version) => new HarnessInfo(key, version), compiledAgainst);
  }

  /// <summary>Gets the registered harnesses, ordered by name.</summary>
  /// <returns>A snapshot of the registrations.</returns>
  public static IReadOnlyCollection<HarnessInfo> GetRegistered()
    => [.. Harnesses.Values.OrderBy(harness => harness.Name, StringComparer.OrdinalIgnoreCase)];
}
