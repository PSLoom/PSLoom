// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Hooks;
using PSLoom.Warp;
using PSLoom.Warp.Hooks;

namespace PSLoom.Cmdlets.Hooks;

/// <summary>
///   Registers a script that runs when a hook fires, wiring the kind on first use. The script receives the
///   <see cref="HookInvocation" /> as <c>$_</c> and as its first argument.
/// </summary>
[Cmdlet(VerbsLifecycle.Register, "Hook")]
[OutputType(typeof(HookRegistration))]
public sealed class RegisterHookCmdlet : PSCmdlet {
  /// <summary>
  ///   Gets or sets the hook kind.
  /// </summary>
  [Parameter(Mandatory = true, Position = 0)]
  public HookKind Kind { get; set; }

  /// <summary>
  ///   Gets or sets the script to run.
  /// </summary>
  [Parameter(Mandatory = true, Position = 1)]
  public ScriptBlock Action { get; set; } = null!;

  /// <summary>
  ///   Gets or sets an optional name; registering the same kind and name again replaces the earlier registration.
  /// </summary>
  [Parameter]
  public string? Name { get; set; }

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      var bus = HookBus.PerRunspace.ForCurrent();
      bus.Wiring.AttachEngine(this.GetEngine());

      var registration = bus.Add(Kind, HookBus.ForScript(Action), Action, Name);

      if (bus.Wiring.EnsureWired(Kind) is { } warning) {
        WriteWarning(warning);
      }

      WriteObject(registration);
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }
}
