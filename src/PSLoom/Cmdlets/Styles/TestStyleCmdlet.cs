// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Styles;
using PSLoom.Warp;

namespace PSLoom.Cmdlets.Styles;

/// <summary>
///   Tests whether a style resolves to an enabled value: <see langword="true" />, a non-zero integer, or
///   <c>true</c>/<c>yes</c>/<c>1</c>/<c>on</c>/<c>enabled</c>.
/// </summary>
[Cmdlet(VerbsDiagnostic.Test, "Style")]
[OutputType(typeof(bool))]
public sealed class TestStyleCmdlet : PSCmdlet {
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

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      WriteObject(StyleTruth.IsTrue(StyleStore.PerRunspace.ForCurrent().Resolve(Context, Name)?.Value));
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }
}
