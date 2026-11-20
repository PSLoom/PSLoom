// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.ManifestTool;

internal static class ManifestCommand {
  internal static int Run(IReadOnlyList<string> arguments, TextWriter error) {
    try {
      var options = ManifestOptions.Parse(arguments);
      var template = File.ReadAllText(options.TemplatePath);
      var cmdlets = CmdletScanner.Scan(options.AssemblyPath);
      var manifest = ManifestRenderer.Render(template, ModuleVersion.Parse(options.Version), cmdlets,
        options.RequiredModules);
      var output = Path.GetFullPath(options.OutputPath);

      Directory.CreateDirectory(Path.GetDirectoryName(output)!);
      File.WriteAllText(output, manifest);

      return 0;
    }
    catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException or
                                        IOException or BadImageFormatException) {
      error.WriteLine(exception.Message);

      return 1;
    }
  }
}
