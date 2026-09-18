// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Styles;
using PSLoom.Warp;

namespace PSLoom.Cmdlets.Styles;

/// <summary>
///   Lists recent style watcher runs, oldest first, with duration and any exception.
/// </summary>
[Cmdlet(VerbsDiagnostic.Trace, "Style")]
[OutputType(typeof(StyleDiagnosticEntry))]
public sealed class TraceStyleCmdlet : PSCmdlet {
  /// <summary>
  ///   Gets or sets the maximum number of most recent entries to return; every kept entry when omitted.
  /// </summary>
  [Parameter(Position = 0)]
  [ValidateRange(0, int.MaxValue)]
  public int? Last { get; set; }

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      WriteObject(StyleStore.PerRunspace.ForCurrent().Diagnostics.Snapshot(Last), true);
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }
}
