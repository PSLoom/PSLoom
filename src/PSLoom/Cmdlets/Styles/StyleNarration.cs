// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Globalization;
using PSLoom.Runtime.Styles;

namespace PSLoom.Cmdlets.Styles;

/// <summary>
///   Reports a style write on a cmdlet's verbose and error streams.
/// </summary>
internal static class StyleNarration {
  /// <summary>
  ///   Narrates the write and each watcher it ran (verbose), then writes one non-terminating error per failed watcher.
  /// </summary>
  public static void Report(PSCmdlet cmdlet, string verb, string context, string name, StyleWriteOutcome outcome) {
    cmdlet.WriteVerbose(
      $"{verb} style '{name}' for '{context}': {Describe(outcome.Previous?.Value, outcome.Previous is not null)} -> " +
      $"{Describe(outcome.Current?.Value, outcome.Current is not null)}.");

    ReportWatchers(cmdlet, outcome.Watchers);
  }

  /// <summary>
  ///   Narrates watcher runs (verbose) and writes one non-terminating error per failed watcher.
  /// </summary>
  public static void ReportWatchers(PSCmdlet cmdlet, IEnumerable<StyleWatcherOutcome> watchers) {
    foreach (var watcher in watchers) {
      var change = watcher.Change;
      var result = watcher.Exception is null ? "ok" : "failed";

      cmdlet.WriteVerbose(
        $"Watcher {watcher.Watcher.Id} ('{change.Name}' on '{change.Context}'): {Describe(change.OldValue, change.OldValue is not null)} -> " +
        $"{Describe(change.NewValue, change.NewValue is not null)}, {result} in {watcher.Elapsed.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)} ms.");

      if (watcher.Exception is not null) {
        cmdlet.WriteError(StyleException.WatcherFailed(watcher).ToErrorRecord());
      }
    }
  }

  private static string Describe(object? value, bool defined)
    => !defined
      ? "<unset>"
      : value switch {
        null => "$null",
        string text => $"'{text}'",
        var _ => LanguagePrimitives.ConvertTo<string>(value)
      };
}
