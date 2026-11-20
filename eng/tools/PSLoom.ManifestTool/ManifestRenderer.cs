// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.ManifestTool;

internal static class ManifestRenderer {
  private const string VERSION_TOKEN = "$version$";
  private const string CMDLETS_TOKEN = "$cmdlets$";
  private const string REQUIRED_MODULES_TOKEN = "$requiredModules$";
  private const string PRERELEASE_TOKEN = "$prerelease$";

  internal static string Render(string template, ModuleVersion version, IReadOnlyCollection<string> cmdlets,
    IReadOnlyCollection<RequiredModule> requiredModules) {
    string[] tokens = [VERSION_TOKEN, CMDLETS_TOKEN, REQUIRED_MODULES_TOKEN, PRERELEASE_TOKEN];

    foreach (var token in tokens)
      if (!template.Contains(token, StringComparison.Ordinal)) {
        throw new InvalidOperationException($"The manifest template has no {token} token.");
      }

    var required = requiredModules.Select(module
      => $"@{{ ModuleName = {Quote(module.Name)}; ModuleVersion = {Quote(module.MinimumVersion)} }}");
    var prerelease = version.Prerelease is null ? string.Empty : $"Prerelease = {Quote(version.Prerelease)}";

    return template
      .Replace(VERSION_TOKEN, version.Numeric, StringComparison.Ordinal)
      .Replace(CMDLETS_TOKEN, string.Join(", ", cmdlets.Select(Quote)), StringComparison.Ordinal)
      .Replace(REQUIRED_MODULES_TOKEN, string.Join(", ", required), StringComparison.Ordinal)
      .Replace(PRERELEASE_TOKEN, prerelease, StringComparison.Ordinal);
  }

  private static string Quote(string value)
    => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
