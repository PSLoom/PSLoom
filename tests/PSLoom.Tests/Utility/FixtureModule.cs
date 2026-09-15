// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.TestKit;

namespace PSLoom.Tests.Utility;

/// <summary>
///   Makes the test modules importable by name: the published harness <c>PSLoom.Fixture</c> and the static modules under
///   <c>TestModules</c>. The process-wide <c>PSModulePath</c> is changed once, never per test.
/// </summary>
internal static class FixtureModule {
  private static readonly Lock _lock = new();
  private static bool _done;

  public static void EnsureOnModulePath() {
    lock (_lock) {
      if (_done) {
        return;
      }

      var current = Environment.GetEnvironmentVariable("PSModulePath") ?? string.Empty;
      var entries = current.Split(Path.PathSeparator);
      var added = new[] { RepositoryLayout.ModulesDirectory, Path.Combine(AppContext.BaseDirectory, "TestModules") }
        .Where(directory => !entries.Contains(directory, StringComparer.OrdinalIgnoreCase));

      Environment.SetEnvironmentVariable("PSModulePath", string.Join(Path.PathSeparator, [.. added, current]));

      _done = true;
    }
  }
}
