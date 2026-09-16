// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Runtime.Sheds;

/// <summary>
///   Decides whether the session will ever draw a prompt, which is what drains the queue. A host that will not applies staged
///   statements at the end of the draft instead.
/// </summary>
internal static class InteractiveHost {
  internal const string OVERRIDE_VARIABLE = "LOOM_INTERACTIVE";
  private const string CONSOLE_HOST = "ConsoleHost";

  public static bool Detect(EngineIntrinsics? engine)
    => Detect(engine?.Host.Name, [.. Environment.GetCommandLineArgs().Skip(1)], Environment.GetEnvironmentVariable(OVERRIDE_VARIABLE));

  internal static bool Detect(string? hostName, IReadOnlyList<string> arguments, string? overrideValue) {
    switch (overrideValue) {
      case "1":
        return true;
      case "0":
        return false;
    }

    if (!string.Equals(hostName, CONSOLE_HOST, StringComparison.Ordinal)) {
      return false;
    }

    var names = arguments.Where(argument => argument.Length > 1 && argument[0] is '-' or '/')
      .Select(argument => argument[1..].ToLowerInvariant())
      .ToHashSet(StringComparer.Ordinal);

    if (names.Contains("noninteractive") ||
        names.Contains("noni")) {
      return false;
    }

    var runsSomething = names.Overlaps(["c", "command", "f", "file", "e", "ec", "encodedcommand"]);
    var staysOpen = names.Contains("noexit") || names.Contains("noe");

    return !runsSomething || staysOpen;
  }
}
