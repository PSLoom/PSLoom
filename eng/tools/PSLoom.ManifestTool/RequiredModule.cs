// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.ManifestTool;

internal sealed record RequiredModule(string Name, string MinimumVersion) {
  internal static RequiredModule Parse(string value) {
    var separator = value.IndexOf('=', StringComparison.Ordinal);

    if (separator <= 0 ||
        separator == value.Length - 1) {
      throw new ArgumentException($"'{value}' is not in the form Name=Version.", nameof(value));
    }

    return new RequiredModule(value[..separator], value[(separator + 1)..]);
  }
}
