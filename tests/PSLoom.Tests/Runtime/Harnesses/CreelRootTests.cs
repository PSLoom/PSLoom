// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Runtime.Harnesses;
using PSLoom.Warp.Storage;

namespace PSLoom.Tests.Runtime.Harnesses;

[TestSubject(typeof(CreelRoot))]
public sealed class CreelRootTests {
  private static readonly string _base = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "creel-root-tests"));

  [Fact]
  public void LoomHome_WinsOnEveryPlatform() {
    var home = Path.Combine(_base, "home");

    CreelRoot.Resolve(Environment(("LOOM_HOME", home), ("LOCALAPPDATA", _base), ("XDG_DATA_HOME", _base)), true).ShouldBe(home);
    CreelRoot.Resolve(Environment(("LOOM_HOME", home), ("XDG_DATA_HOME", _base)), false).ShouldBe(home);
  }

  [Fact]
  public void Windows_UsesLocalAppData()
    => CreelRoot.Resolve(Environment(("LOCALAPPDATA", _base)), true).ShouldBe(Path.Combine(_base, "Loom"));

  [Fact]
  public void Unix_UsesXdgDataHome_ThenHome() {
    CreelRoot.Resolve(Environment(("XDG_DATA_HOME", _base), ("HOME", "/ignored")), false).ShouldBe(Path.Combine(_base, "loom"));
    CreelRoot.Resolve(Environment(("HOME", _base)), false).ShouldBe(Path.Combine(_base, ".local", "share", "loom"));
  }

  [Fact]
  public void RelativeOrBlankVariables_AreIgnored() {
    CreelRoot.Resolve(Environment(("LOOM_HOME", "relative/path"), ("XDG_DATA_HOME", " "), ("HOME", _base)), false)
      .ShouldBe(Path.Combine(_base, ".local", "share", "loom"));
  }

  [Fact]
  public void ForHarness_IsARootOfItsOwn() {
    var storage = CreelRoot.ForHarness("Crates");

    storage.IsRoot.ShouldBeTrue();
    storage.FullPath.ShouldEndWith(Path.Combine(CreelRoot.HARNESSES_DIRECTORY, "Crates"));
    Should.Throw<CreelPathException>(() => storage.Combine("../Other"));
  }

  private static Func<string, string?> Environment(params (string Name, string Value)[] variables)
    => name => variables.FirstOrDefault(variable => variable.Name == name).Value;
}
