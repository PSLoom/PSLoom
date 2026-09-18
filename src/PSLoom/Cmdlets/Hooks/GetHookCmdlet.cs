// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Hooks;
using PSLoom.Warp;
using PSLoom.Warp.Hooks;

namespace PSLoom.Cmdlets.Hooks;

/// <summary>
///   Lists hook registrations in registration order, optionally filtered by kind and name.
/// </summary>
[Cmdlet(VerbsCommon.Get, "Hook")]
[OutputType(typeof(HookRegistration))]
public sealed class GetHookCmdlet : PSCmdlet {
  /// <summary>
  ///   Gets or sets the kind to filter by.
  /// </summary>
  [Parameter(Position = 0)]
  public HookKind? Kind { get; set; }

  /// <summary>
  ///   Gets or sets the name to filter by (case-insensitive).
  /// </summary>
  [Parameter]
  public string? Name { get; set; }

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      WriteObject(HookBus.PerRunspace.ForCurrent().Get(Kind, Name), true);
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }
}
