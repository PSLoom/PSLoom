// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.TestKit;

/// <summary>Locates the repository a test assembly was built from.</summary>
public static class RepoLayout {
  private const string MARKER = "Version.props";

  /// <summary>Finds the nearest directory above <paramref name="start" /> that holds <c>Version.props</c>.</summary>
  /// <param name="start">Where to start; the directory of the running test assembly when omitted.</param>
  /// <returns>The full path of the repository root.</returns>
  /// <exception cref="DirectoryNotFoundException">No such directory exists.</exception>
  public static string FindRoot(string? start = null) {
    var origin = start ?? AppContext.BaseDirectory;

    for (var directory = new DirectoryInfo(origin); directory is not null; directory = directory.Parent)
      if (File.Exists(Path.Combine(directory.FullName, MARKER))) {
        return directory.FullName;
      }

    throw new DirectoryNotFoundException($"No directory that holds {MARKER} was found above '{origin}'.");
  }

  /// <summary>Gets the directory where the build assembles the modules of a repository.</summary>
  /// <param name="root">The repository root.</param>
  /// <returns>The path of <c>artifacts/modules</c> under the root.</returns>
  public static string ModulesDirectory(string root)
    => Path.Combine(root, "artifacts", "modules");

  /// <summary>
  ///   Gets the directories a repository resolves built modules from: its own, then one per neighbor repository
  ///   pinned by a <c>&lt;Repository&gt;.ref</c> file in its root.
  /// </summary>
  /// <param name="root">The repository root.</param>
  /// <returns>The module directories, the repository's own first.</returns>
  public static IReadOnlyList<string> ModulePaths(string root) {
    var neighbors = Directory.EnumerateFiles(root, "*.ref").Select(Path.GetFileNameWithoutExtension)
      .Order(StringComparer.Ordinal)
      .Select(name => ModulesDirectory(Path.GetFullPath(Path.Combine(root, "..", name!))));

    return [ModulesDirectory(root), .. neighbors];
  }
}
