// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Warp.Storage;

namespace PSLoom.Warp.Tests.Storage;

[TestSubject(typeof(CreelPath))]
public sealed class CreelPathTests : IDisposable {
  private readonly string _sandbox = Directory.CreateTempSubdirectory("creel-").FullName;

  public void Dispose()
    => Directory.Delete(_sandbox, true);

  [Fact]
  public void CombineResolvesUnderRoot() {
    var root = CreelPath.CreateRoot(Path.Combine(_sandbox, "root"));

    var path = root / "plugins/owner/repo";

    path.FullPath.ShouldBe(Path.Combine(_sandbox, "root", "plugins", "owner", "repo"));
    path.RelativePath.ShouldBe(Path.Combine("plugins", "owner", "repo"));
    path.Name.ShouldBe("repo");
  }

  [Fact]
  public void EmptyInputReturnsSamePath() {
    var root = CreelPath.CreateRoot(_sandbox);

    root.Combine(string.Empty).ShouldBe(root);
  }

  [Theory]
  [InlineData("..")]
  [InlineData("../sibling")]
  [InlineData("a/../../b")]
  [InlineData("a/b/../../../c")]
  public void TraversalOutsideRootIsRejected(string input) {
    var root = CreelPath.CreateRoot(Path.Combine(_sandbox, "root"));

    Should.Throw<CreelPathException>(() => root.Combine(input)).Violation.ShouldBe(CreelPathViolation.Escape);
  }

  [Fact]
  public void TraversalThatStaysInsideRootIsAccepted() {
    var root = CreelPath.CreateRoot(Path.Combine(_sandbox, "root"));

    (root / "a/../b").FullPath.ShouldBe(Path.Combine(_sandbox, "root", "b"));
  }

  [Fact]
  public void SiblingSharingThePrefixIsRejected() {
    var root = CreelPath.CreateRoot(Path.Combine(_sandbox, "root"));

    Should.Throw<CreelPathException>(() => root.Combine("../root-evil/x")).Violation.ShouldBe(CreelPathViolation.Escape);
  }

  [Fact]
  public void RootedInputIsRejected() {
    var root = CreelPath.CreateRoot(_sandbox);

    Should.Throw<CreelPathException>(() => root.Combine(Path.GetFullPath(_sandbox))).Violation.ShouldBe(CreelPathViolation.Rooted);
  }

  [Fact]
  public void NulCharacterIsRejected() {
    var root = CreelPath.CreateRoot(_sandbox);

    Should.Throw<CreelPathException>(() => root.Combine("a\0b")).Violation.ShouldBe(CreelPathViolation.InvalidCharacters);
  }

  [Fact]
  public void ParentNeverLeavesRoot() {
    var root = CreelPath.CreateRoot(_sandbox);

    (root / "a").Parent.ShouldBe(root);
    root.Parent.ShouldBeNull();
  }

  [Fact]
  public void DefaultValueThrowsOnUse() {
    var path = default(CreelPath);

    path.IsDefault.ShouldBeTrue();
    Should.Throw<InvalidOperationException>(() => path.FullPath);
  }

  [Fact]
  public void LinkPointingOutsideRootIsRejected() {
    var root = CreelPath.CreateRoot(Directory.CreateDirectory(Path.Combine(_sandbox, "root")).FullName);
    var outside = Directory.CreateDirectory(Path.Combine(_sandbox, "outside")).FullName;

    if (!TryCreateDirectoryLink(Path.Combine(root.FullPath, "escape"), outside)) {
      Assert.Skip("Creating a directory symbolic link is not permitted in this environment.");
    }

    Should.Throw<CreelPathException>(() => root.Combine("escape/file.txt")).Violation.ShouldBe(CreelPathViolation.LinkEscape);
  }

  [Fact]
  public void LinkPointingInsideRootIsAccepted() {
    var root = CreelPath.CreateRoot(Directory.CreateDirectory(Path.Combine(_sandbox, "root")).FullName);
    var inside = Directory.CreateDirectory(Path.Combine(root.FullPath, "real")).FullName;

    if (!TryCreateDirectoryLink(Path.Combine(root.FullPath, "alias"), inside)) {
      Assert.Skip("Creating a directory symbolic link is not permitted in this environment.");
    }

    (root / "alias/file.txt").Name.ShouldBe("file.txt");
  }

  private static bool TryCreateDirectoryLink(string path, string target) {
    try {
      Directory.CreateSymbolicLink(path, target);
      return true;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
      return false;
    }
  }
}
