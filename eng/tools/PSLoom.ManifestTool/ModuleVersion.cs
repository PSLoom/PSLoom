// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.ManifestTool;

/// <summary>
///   A semantic version split the way a module manifest needs it: a numeric <c>ModuleVersion</c> and a
///   <c>Prerelease</c> label, which PowerShell accepts only as ASCII letters and digits.
/// </summary>
internal sealed record ModuleVersion(string Numeric, string? Prerelease) {
  internal static ModuleVersion Parse(string version) {
    var metadata = version.IndexOf('+', StringComparison.Ordinal);
    var core = metadata < 0 ? version : version[..metadata];
    var dash = core.IndexOf('-', StringComparison.Ordinal);
    var numeric = dash < 0 ? core : core[..dash];

    if (!Version.TryParse(numeric, out var parsed) ||
        parsed.Build < 0 ||
        parsed.Revision >= 0) {
      throw new FormatException($"'{version}' is not a MAJOR.MINOR.PATCH version.");
    }

    if (dash < 0) {
      return new ModuleVersion(numeric, null);
    }

    var label = string.Concat(core[(dash + 1)..].Where(char.IsAsciiLetterOrDigit));

    if (label.Length == 0) {
      throw new FormatException($"'{version}' has an empty pre-release label.");
    }

    return new ModuleVersion(numeric, label);
  }
}
