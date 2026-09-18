// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Styles;
using PSLoom.Warp;

namespace PSLoom.Cmdlets.Styles;

/// <summary>
///   Resolves a style value for a concrete context. Writes nothing when no pattern matches, unless <see cref="Default" /> is given.
/// </summary>
[Cmdlet(VerbsCommon.Get, "Style")]
public sealed class GetStyleCmdlet : PSCmdlet {
  /// <summary>
  ///   Gets or sets the concrete context (not a pattern).
  /// </summary>
  [Parameter(Mandatory = true, Position = 0)]
  public string Context { get; set; } = null!;

  /// <summary>
  ///   Gets or sets the style name.
  /// </summary>
  [Parameter(Mandatory = true, Position = 1)]
  public string Name { get; set; } = null!;

  /// <summary>
  ///   Gets or sets the value written when nothing resolves.
  /// </summary>
  [Parameter]
  [AllowNull]
  public object? Default { get; set; }

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      if (StyleStore.PerRunspace.ForCurrent().Resolve(Context, Name) is { } definition) {
        WriteObject(definition.Value);
      }
      else if (MyInvocation.BoundParameters.ContainsKey(nameof(Default))) {
        WriteObject(Default);
      }
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }
}
