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
}
