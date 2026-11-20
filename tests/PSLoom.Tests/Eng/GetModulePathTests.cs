// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.TestKit;

namespace PSLoom.Tests.Eng;

public sealed class GetModulePathTests {
  private readonly static string Script = Path.Combine(RepoLayout.FindRoot(), "eng", "Get-ModulePath.ps1");

  [Fact]
  public void TheDirectoriesThatExistAreListedOwnFirst() {
    var workspace = Directory.CreateTempSubdirectory("psloom-modulepath-");

    try {
      var root = Directory.CreateDirectory(Path.Combine(workspace.FullName, "Harness")).FullName;
      var own = Directory.CreateDirectory(Path.Combine(root, "artifacts", "modules")).FullName;
      var neighbor = Directory.CreateDirectory(Path.Combine(workspace.FullName, "Base", "artifacts", "modules"))
        .FullName;
      File.WriteAllText(Path.Combine(root, "Base.ref"), "v1.0.0");
      File.WriteAllText(Path.Combine(root, "NotCloned.ref"), "v1.0.0");

      var result = PwshProcess.RunFile(Script, ["-Path", root]);
      var lines = result.StandardOutput.Split('\n',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

      Assert.Equal(0, result.ExitCode);
      Assert.Equal(new[] { own, neighbor }, lines);
    }
    finally {
      workspace.Delete(true);
    }
  }
}
