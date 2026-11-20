// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.TestKit;

namespace PSLoom.Tests.TestKit;

public sealed class PwshProcessTests {
  [Fact]
  public void RunCapturesStandardOutput() {
    var result = PwshProcess.Run("'hello'");

    Assert.Equal(0, result.ExitCode);
    Assert.Equal("hello", result.StandardOutput.Trim());
  }

  [Fact]
  public void RunCapturesStandardError() {
    var result = PwshProcess.Run("[Console]::Error.WriteLine('oops')");

    Assert.Equal(0, result.ExitCode);
    Assert.Equal("oops", result.StandardError.Trim());
  }

  [Fact]
  public void RunReportsTheExitCode() {
    var result = PwshProcess.Run("exit 3");

    Assert.Equal(3, result.ExitCode);
  }

  [Fact]
  public void RunPutsTheModulePathsFirst() {
    var directory = Directory.CreateTempSubdirectory("psloom-testkit-");

    try {
      var result = PwshProcess.Run("$env:PSModulePath.Split([System.IO.Path]::PathSeparator)[0]", [directory.FullName]);

      Assert.Equal(directory.FullName, result.StandardOutput.Trim());
    }
    finally {
      directory.Delete(true);
    }
  }

  [Fact]
  public void RunFilePassesArguments() {
    var directory = Directory.CreateTempSubdirectory("psloom-testkit-");

    try {
      var script = Path.Combine(directory.FullName, "echo.ps1");
      File.WriteAllText(script, "param([string] $Value) \"got $Value\"");

      var result = PwshProcess.RunFile(script, ["-Value", "x"]);

      Assert.Equal(0, result.ExitCode);
      Assert.Equal("got x", result.StandardOutput.Trim());
    }
    finally {
      directory.Delete(true);
    }
  }
}
