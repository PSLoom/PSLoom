// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Styles;
using PSLoom.Warp;

namespace PSLoom.Cmdlets.Styles;

/// <summary>
///   Defines or redefines a style value for a context pattern, then runs the watchers the write affects.
/// </summary>
[Cmdlet(VerbsCommon.Set, "Style")]
public sealed class SetStyleCmdlet : PSCmdlet {
  /// <summary>
  ///   Gets or sets the context pattern (PowerShell wildcard syntax, case-sensitive).
  /// </summary>
  [Parameter(Mandatory = true, Position = 0)]
  public string Context { get; set; } = null!;

  /// <summary>
  ///   Gets or sets the style name.
  /// </summary>
  [Parameter(Mandatory = true, Position = 1)]
  public string Name { get; set; } = null!;

  /// <summary>
  ///   Gets or sets the value.
  /// </summary>
  [Parameter(Mandatory = true, Position = 2)]
  [AllowNull]
  [AllowEmptyString]
  [AllowEmptyCollection]
  public object? Value { get; set; }

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      var outcome = StyleStore.PerRunspace.ForCurrent().SetCore(Context, Name, Value);
      StyleNarration.Report(this, "Set", Context, Name, outcome);
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }
}
