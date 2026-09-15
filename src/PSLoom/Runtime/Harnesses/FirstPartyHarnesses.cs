// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Runtime.Harnesses;

/// <summary>
///   The harnesses <c>Thread</c> may load and install. Each is published as module <c>PSLoom.&lt;Name&gt;</c>.
/// </summary>
internal static class FirstPartyHarnesses {
  internal const string MODULE_PREFIX = "PSLoom.";

  /// <summary>
  ///   Gets the first-party harness names.
  /// </summary>
  public static IReadOnlyList<string> Names { get; } = ["Reed", "Colorway", "Weft", "Shuttle"];

  /// <summary>
  ///   Creates the mutable, case-insensitive set a session starts with.
  /// </summary>
  public static HashSet<string> CreateSet()
    => new(Names, StringComparer.OrdinalIgnoreCase);

  public static string ModuleName(string harnessName)
    => MODULE_PREFIX + harnessName;
}
