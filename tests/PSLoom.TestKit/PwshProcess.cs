// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Text;

namespace PSLoom.TestKit;

/// <summary>Runs scripts in a separate <c>pwsh -NoProfile</c> process, the way a user session loads modules.</summary>
public static class PwshProcess {
  private readonly static TimeSpan Timeout = TimeSpan.FromSeconds(120);

  /// <summary>Runs a script.</summary>
  /// <param name="script">The script text.</param>
  /// <param name="modulePaths">
  ///   Directories put at the front of <c>PSModulePath</c> inside the session, so that they win over installed modules.
  /// </param>
  /// <returns>The exit code and the captured streams.</returns>
  public static PwshResult Run(string script, IEnumerable<string>? modulePaths = null) {
    ArgumentNullException.ThrowIfNull(script);

    var paths = modulePaths?.ToArray() ?? [];
    var prelude = paths.Length == 0
      ? string.Empty
      : $"$env:PSModulePath = (@({string.Join(", ", paths.Select(Quote))}) + $env:PSModulePath) -join [System.IO.Path]::PathSeparator\n";
    var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(prelude + script));

    return Start(["-EncodedCommand", encoded], null);
  }

  /// <summary>Runs a script file.</summary>
  /// <param name="scriptPath">The path of the script.</param>
  /// <param name="arguments">The arguments passed to the script.</param>
  /// <param name="workingDirectory">The working directory; the current one when omitted.</param>
  /// <returns>The exit code and the captured streams.</returns>
  public static PwshResult RunFile(string scriptPath, IEnumerable<string>? arguments = null,
    string? workingDirectory = null) {
    ArgumentNullException.ThrowIfNull(scriptPath);

    return Start(["-File", scriptPath, .. arguments ?? []], workingDirectory);
  }

  private static string Quote(string value)
    => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

  private static PwshResult Start(IEnumerable<string> arguments, string? workingDirectory) {
    var startInfo = new ProcessStartInfo("pwsh") {
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    if (workingDirectory is not null) {
      startInfo.WorkingDirectory = workingDirectory;
    }

    string[] fixedArguments = ["-NoLogo", "-NoProfile", "-NonInteractive", "-OutputFormat", "Text"];
    foreach (var argument in fixedArguments.Concat(arguments)) startInfo.ArgumentList.Add(argument);

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("pwsh could not be started.");
    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();

    if (!process.WaitForExit(Timeout)) {
      process.Kill(true);
      throw new TimeoutException("pwsh did not exit within the time limit.");
    }

    return new PwshResult(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
  }
}
