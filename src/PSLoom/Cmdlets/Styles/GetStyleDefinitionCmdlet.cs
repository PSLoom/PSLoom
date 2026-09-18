// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Styles;
using PSLoom.Warp;
using PSLoom.Warp.Styles;

namespace PSLoom.Cmdlets.Styles;

/// <summary>
///   Lists stored style definitions, optionally filtered by exact context pattern and name, in definition order.
/// </summary>
[Cmdlet(VerbsCommon.Get, "StyleDefinition")]
[OutputType(typeof(StyleDefinition))]
public sealed class GetStyleDefinitionCmdlet : PSCmdlet {
  /// <summary>
  ///   Gets or sets the exact context pattern to filter by.
  /// </summary>
  [Parameter(Position = 0)]
  public string? Context { get; set; }

  /// <summary>
  ///   Gets or sets the style name to filter by.
  /// </summary>
  [Parameter(Position = 1)]
  public string? Name { get; set; }

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      WriteObject(StyleStore.PerRunspace.ForCurrent().GetDefinitions(Context, Name), true);
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }
}
