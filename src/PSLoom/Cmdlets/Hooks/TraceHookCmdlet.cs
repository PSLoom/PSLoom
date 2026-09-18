// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Hooks;
using PSLoom.Warp;

namespace PSLoom.Cmdlets.Hooks;

/// <summary>
///   Lists recent hook invocations, oldest first, with duration, slow flag and any exception.
/// </summary>
[Cmdlet(VerbsDiagnostic.Trace, "Hook")]
[OutputType(typeof(HookDiagnosticEntry))]
public sealed class TraceHookCmdlet : PSCmdlet {
  /// <summary>
  ///   Gets or sets the maximum number of most recent entries to return; every kept entry when omitted.
  /// </summary>
  [Parameter(Position = 0)]
  [ValidateRange(0, int.MaxValue)]
  public int? Last { get; set; }

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      WriteObject(HookBus.PerRunspace.ForCurrent().Diagnostics.Snapshot(Last), true);
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }
}
