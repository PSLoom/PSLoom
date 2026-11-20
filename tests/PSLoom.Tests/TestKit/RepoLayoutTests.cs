// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.TestKit;

namespace PSLoom.Tests.TestKit;

public sealed class RepoLayoutTests {
  [Fact]
  public void FindRootReturnsTheDirectoryThatHoldsVersionProps() {
    var root = RepoLayout.FindRoot();

    Assert.True(File.Exists(Path.Combine(root, "Version.props")));
    Assert.True(File.Exists(Path.Combine(root, "PSLoom.slnx")));
  }

  [Fact]
  public void FindRootFailsOutsideARepository() {
    var directory = Directory.CreateTempSubdirectory("psloom-testkit-");

    try {
      Assert.Throws<DirectoryNotFoundException>(() => RepoLayout.FindRoot(directory.FullName));
    }
    finally {
      directory.Delete(true);
    }
  }

  [Fact]
  public void ModulesDirectoryIsUnderArtifacts() {
    Assert.Equal(Path.Combine("repo", "artifacts", "modules"), RepoLayout.ModulesDirectory("repo"));
  }

  [Fact]
  public void ModulePathsOfARepositoryWithoutPinsIsItsOwnDirectory() {
    var directory = Directory.CreateTempSubdirectory("psloom-testkit-");

    try {
      var paths = RepoLayout.ModulePaths(directory.FullName);

      Assert.Equal(RepoLayout.ModulesDirectory(directory.FullName), Assert.Single(paths));
    }
    finally {
      directory.Delete(true);
    }
  }

  [Fact]
  public void ModulePathsAddsOneNeighborPerPinFile() {
    var workspace = Directory.CreateTempSubdirectory("psloom-testkit-");

    try {
      var root = Directory.CreateDirectory(Path.Combine(workspace.FullName, "Harness")).FullName;
      File.WriteAllText(Path.Combine(root, "Other.ref"), "v1.0.0");
      File.WriteAllText(Path.Combine(root, "Base.ref"), "v1.0.0");
      string[] expected = [
        RepoLayout.ModulesDirectory(root), RepoLayout.ModulesDirectory(Path.Combine(workspace.FullName, "Base")),
        RepoLayout.ModulesDirectory(Path.Combine(workspace.FullName, "Other"))
      ];

      Assert.Equal(expected, RepoLayout.ModulePaths(root));
    }
    finally {
      workspace.Delete(true);
    }
  }
}
