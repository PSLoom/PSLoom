// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.TestKit;

namespace PSLoom.Tests.Architecture;

public sealed class RepositoryLayoutTests {
  [Fact]
  public void RootIsAnAncestorWithASolution() {
    Directory.EnumerateFiles(RepositoryLayout.Root, "*.slnx").ShouldNotBeEmpty();
    Path.GetRelativePath(RepositoryLayout.Root, AppContext.BaseDirectory).ShouldNotStartWith("..");
  }

  [Fact]
  public void FindsTheNearestSolutionRegardlessOfItsName() {
    var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    var nested = Path.Combine(root, "nested");
    var output = Path.Combine(nested, "bin", "Release");
    Directory.CreateDirectory(output);
    try {
      File.WriteAllText(Path.Combine(root, "Outer.slnx"), "<Solution />");
      File.WriteAllText(Path.Combine(nested, "Unrelated.Name.slnx"), "<Solution />");
      RepositoryLayout.FindRoot(output).ShouldBe(nested);
      File.Delete(Path.Combine(nested, "Unrelated.Name.slnx"));
      RepositoryLayout.FindRoot(output).ShouldBe(root);
    }
    finally {
      Directory.Delete(root, true);
    }
  }

  [Fact]
  public void ReportsMissingSolutionAtTheFilesystemRoot() {
    var root = Path.GetPathRoot(RepositoryLayout.Root)!;
    Should.Throw<InvalidOperationException>(() => RepositoryLayout.FindRoot(root)).Message.ShouldContain("*.slnx");
  }
}
