// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Storage;

namespace PSLoom.Runtime.Harnesses;

/// <summary>
///   Locates the Creel, the single storage root under which every harness gets its own directory. Nothing is created here.
/// </summary>
internal static class CreelRoot {
  internal const string HOME_VARIABLE = "LOOM_HOME";
  internal const string HARNESSES_DIRECTORY = "harnesses";

  /// <summary>
  ///   Resolves the root for the current process.
  /// </summary>
  public static string Resolve()
    => Resolve(Environment.GetEnvironmentVariable, OperatingSystem.IsWindows());

  /// <summary>
  ///   Resolves the root: <c>$env:LOOM_HOME</c>; else <c>%LOCALAPPDATA%\Loom</c> on Windows; else <c>$XDG_DATA_HOME/loom</c>; else
  ///   <c>~/.local/share/loom</c>.
  /// </summary>
  internal static string Resolve(Func<string, string?> environment, bool isWindows) {
    if (Absolute(environment(HOME_VARIABLE)) is { } home) {
      return home;
    }

    if (isWindows) {
      var localAppData = Absolute(environment("LOCALAPPDATA")) ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
      return Path.Combine(localAppData, "Loom");
    }

    if (Absolute(environment("XDG_DATA_HOME")) is { } dataHome) {
      return Path.Combine(dataHome, "loom");
    }

    var userHome = Absolute(environment("HOME")) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    return Path.Combine(userHome, ".local", "share", "loom");
  }

  /// <summary>
  ///   Gets the storage root of one harness. It is a root of its own, so a harness cannot combine into a sibling's directory.
  /// </summary>
  public static CreelPath ForHarness(string harnessName)
    => CreelPath.CreateRoot(Path.Combine(Resolve(), HARNESSES_DIRECTORY, harnessName));

  private static string? Absolute(string? path)
    => string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ? null : Path.GetFullPath(path);
}
