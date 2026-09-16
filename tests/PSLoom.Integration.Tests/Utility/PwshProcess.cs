// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PSLoom.TestKit;

namespace PSLoom.Integration.Tests.Utility;

/// <summary>
///   Runs a script in a fresh <c>pwsh</c> process against the published modules, the way a user's profile loads them: no profile,
///   <c>artifacts/modules</c> first on <c>PSModulePath</c>, and a private <c>LOOM_HOME</c> so nothing touches the real Creel.
/// </summary>
internal static class PwshProcess {
  private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(2);
  private static readonly Lazy<string?> _executable = new(Find);

  /// <summary>
  ///   Gets a value indicating whether <c>pwsh</c> is on <c>PATH</c>.
  /// </summary>
  public static bool IsAvailable => _executable.Value is not null;

  /// <summary>
  ///   Runs a script and returns what the process did.
  /// </summary>
  public static PwshResult Run(string script) {
    Assert.SkipUnless(IsAvailable, "pwsh is not on PATH; the published-module tests need a real PowerShell process.");

    var home = Directory.CreateTempSubdirectory("psloom-integration-").FullName;

    try {
      var start = new ProcessStartInfo(_executable.Value!) {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
      };

      start.ArgumentList.Add("-NoLogo");
      start.ArgumentList.Add("-NoProfile");
      start.ArgumentList.Add("-NonInteractive");
      start.ArgumentList.Add("-EncodedCommand");
      start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));

      start.Environment["PSModulePath"] = string.Join(Path.PathSeparator, RepositoryLayout.ModulesDirectory,
        Environment.GetEnvironmentVariable("PSModulePath") ?? string.Empty);
      start.Environment["LOOM_HOME"] = home;

      using var process = Process.Start(start)!;
      var output = process.StandardOutput.ReadToEndAsync();
      var error = process.StandardError.ReadToEndAsync();

      if (!process.WaitForExit(_timeout)) {
        process.Kill(true);
        throw new TimeoutException($"pwsh did not finish within {_timeout}:\n{script}");
      }

      return new PwshResult(process.ExitCode, output.Result, error.Result);
    }
    finally {
      Directory.Delete(home, true);
    }
  }

  private static string? Find() {
    var name = OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh";

    return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
      .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
      .Select(directory => Path.Combine(directory, name))
      .FirstOrDefault(File.Exists);
  }
}

/// <summary>
///   What a <c>pwsh</c> process did.
/// </summary>
internal sealed record PwshResult(int ExitCode, string Output, string Error) {
  /// <summary>
  ///   Parses the last output line as JSON, where the scripts put their findings.
  /// </summary>
  public JsonElement Json() {
    var line = Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault()
               ?? throw new InvalidOperationException($"pwsh wrote nothing.\nstderr:\n{Error}");

    try {
      return JsonDocument.Parse(line).RootElement.Clone();
    }
    catch (JsonException exception) {
      throw new InvalidOperationException($"pwsh's last line is not JSON: {line}\nstderr:\n{Error}", exception);
    }
  }
}
