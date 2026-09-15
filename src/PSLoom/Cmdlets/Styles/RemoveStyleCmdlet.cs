// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Styles;
using PSLoom.Warp;

namespace PSLoom.Cmdlets.Styles;

/// <summary>
///   Removes the style value defined for an exact context pattern, then runs the watchers the removal affects.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "Style")]
public sealed class RemoveStyleCmdlet : PSCmdlet {
  /// <summary>
  ///   Gets or sets the exact context pattern the value was defined for.
  /// </summary>
  [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
  public string Context { get; set; } = null!;

  /// <summary>
  ///   Gets or sets the style name.
  /// </summary>
  [Parameter(Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true)]
  public string Name { get; set; } = null!;

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      var outcome = StyleStore.PerRunspace.ForCurrent().RemoveCore(Context, Name);

      if (!outcome.Applied) {
        WriteWarning($"No style '{Name}' is defined for the context pattern '{Context}'.");
        return;
      }

      StyleNarration.Report(this, "Removed", Context, Name, outcome);
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }
}
