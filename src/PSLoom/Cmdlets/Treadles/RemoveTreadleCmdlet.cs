// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Treadles;
using PSLoom.Warp;

namespace PSLoom.Cmdlets.Treadles;

/// <summary>
///   Removes treadles and the functions they installed. A same-named command PSLoom did not define is left alone.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "Treadle")]
[OutputType(typeof(void))]
public sealed class RemoveTreadleCmdlet : PSCmdlet {
  /// <summary>
  ///   Gets or sets the names to remove.
  /// </summary>
  [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
  [ValidateNotNullOrEmpty]
  public string[] Name { get; set; } = null!;

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      var catalog = TreadleCatalog.PerRunspace.ForCurrent();
      var engine = this.GetEngine();

      foreach (var name in Name) {
        if (catalog.Remove(engine, name) is not { } outcome) {
          WriteWarning($"No treadle named '{name}' is defined in this session.");
          continue;
        }

        WriteVerbose($"Removed treadle '{outcome.Name}'.");
        TreadleNarration.ReportSubscribers(this, outcome);
      }
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }
}
