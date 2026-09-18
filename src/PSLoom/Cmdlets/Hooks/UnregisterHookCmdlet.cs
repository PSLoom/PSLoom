// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Hooks;
using PSLoom.Warp;
using PSLoom.Warp.Hooks;

namespace PSLoom.Cmdlets.Hooks;

/// <summary>
///   Removes a hook registration by identifier, or by kind and name. The kind stays wired.
/// </summary>
[Cmdlet(VerbsLifecycle.Unregister, "Hook", DefaultParameterSetName = BY_ID)]
public sealed class UnregisterHookCmdlet : PSCmdlet {
  private const string BY_ID = "ById";
  private const string BY_NAME = "ByName";

  /// <summary>
  ///   Gets or sets the registration identifier.
  /// </summary>
  [Parameter(Mandatory = true, Position = 0, ParameterSetName = BY_ID, ValueFromPipelineByPropertyName = true)]
  public Guid Id { get; set; }

  /// <summary>
  ///   Gets or sets the kind of the named registration.
  /// </summary>
  [Parameter(Mandatory = true, ParameterSetName = BY_NAME)]
  public HookKind Kind { get; set; }

  /// <summary>
  ///   Gets or sets the registration name.
  /// </summary>
  [Parameter(Mandatory = true, ParameterSetName = BY_NAME)]
  public string Name { get; set; } = null!;

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      var bus = HookBus.PerRunspace.ForCurrent();
      var removed = ParameterSetName == BY_ID ? bus.Remove(Id) : bus.Remove(Kind, Name);

      if (!removed) {
        WriteWarning(ParameterSetName == BY_ID
          ? $"No hook registration with Id '{Id}' was found."
          : $"No '{Kind}' hook registration named '{Name}' was found.");
      }
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }
}
