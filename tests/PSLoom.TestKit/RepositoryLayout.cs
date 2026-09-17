// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Collections;
using System.Management.Automation.Language;
using System.Xml.Linq;

namespace PSLoom.TestKit;

/// <summary>
///   Locates repository files and published module layouts from a running test assembly.
/// </summary>
public static class RepositoryLayout {
  private const string SOLUTION_FILE_PATTERN = "*.slnx";

  /// <summary>
  ///   Gets the absolute path of the repository root, the nearest directory containing a solution file.
  /// </summary>
  public static string Root { get; } = FindRoot(AppContext.BaseDirectory);

  /// <summary>
  ///   Gets the absolute path of the directory <c>PublishModule</c> writes module layouts to.
  /// </summary>
  public static string ModulesDirectory => Path.Combine(Root, "artifacts", "modules");

  /// <summary>
  ///   Loads a project file relative to the repository root.
  /// </summary>
  /// <param name="relativePath">The project file path, relative to the repository root.</param>
  /// <returns>The parsed project file.</returns>
  public static XDocument LoadProject(string relativePath)
    => XDocument.Load(Path.Combine(Root, relativePath));

  /// <summary>
  ///   Gets the versioned directory of a published module, <c>artifacts/modules/&lt;name&gt;/&lt;version&gt;</c>.
  /// </summary>
  /// <param name="moduleName">The module name.</param>
  /// <returns>The absolute path of the single published version directory.</returns>
  /// <exception cref="InvalidOperationException">Thrown when the module is not published exactly once.</exception>
  public static string GetPublishedModuleDirectory(string moduleName) {
    var moduleRoot = Path.Combine(ModulesDirectory, moduleName);
    var versions = Directory.Exists(moduleRoot) ? Directory.GetDirectories(moduleRoot) : [];

    return versions.Length == 1
      ? versions[0]
      : throw new InvalidOperationException(
        $"Expected exactly one published version of '{moduleName}' under '{moduleRoot}', found {versions.Length}. Build the solution first.");
  }

  /// <summary>
  ///   Reads a PowerShell data file (such as a module manifest) without executing it.
  /// </summary>
  /// <param name="path">The absolute path of the data file.</param>
  /// <returns>The top-level hashtable.</returns>
  /// <exception cref="InvalidOperationException">Thrown when the file does not parse to a single hashtable literal.</exception>
  public static Hashtable ReadDataFile(string path) {
    var ast = Parser.ParseFile(path, out var _, out var errors);

    if (errors.Length > 0) {
      throw new InvalidOperationException($"'{path}' has parse errors: {errors[0].Message}");
    }

    return ast.Find(node => node is HashtableAst, false) is HashtableAst hashtable
      ? (Hashtable)hashtable.SafeGetValue()
      : throw new InvalidOperationException($"'{path}' does not contain a hashtable literal.");
  }

  internal static string FindRoot(string startDirectory) {
    for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent) {
      if (directory.EnumerateFiles(SOLUTION_FILE_PATTERN).Any()) {
        return directory.FullName;
      }
    }

    throw new InvalidOperationException($"Could not find '{SOLUTION_FILE_PATTERN}' above '{startDirectory}'.");
  }
}
