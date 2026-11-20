// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.ManifestTool;

internal sealed record ManifestOptions(
  string AssemblyPath,
  string TemplatePath,
  string OutputPath,
  string Version,
  IReadOnlyList<RequiredModule> RequiredModules) {
  private const string ASSEMBLY = "--assembly";
  private const string TEMPLATE = "--template";
  private const string OUTPUT = "--output";
  private const string VERSION = "--version";
  private const string REQUIRED_MODULE = "--required-module";

  internal static ManifestOptions Parse(IReadOnlyList<string> arguments) {
    if (arguments.Count % 2 != 0) {
      throw new ArgumentException("Every option needs a value.", nameof(arguments));
    }

    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    var requiredModules = new List<RequiredModule>();

    for (var index = 0; index < arguments.Count; index += 2) {
      var name = arguments[index];
      var value = arguments[index + 1];

      if (string.Equals(name, REQUIRED_MODULE, StringComparison.Ordinal)) {
        requiredModules.Add(RequiredModule.Parse(value));
        continue;
      }

      if (name is not (ASSEMBLY or TEMPLATE or OUTPUT or VERSION)) {
        throw new ArgumentException($"Unknown option '{name}'.", nameof(arguments));
      }

      values[name] = value;
    }

    return new ManifestOptions(Get(ASSEMBLY), Get(TEMPLATE), Get(OUTPUT), Get(VERSION), requiredModules);

    string Get(string name) {
      if (!values.TryGetValue(name, out var value)) {
        throw new ArgumentException($"Missing option '{name}'.", nameof(arguments));
      }

      return value;
    }
  }
}
