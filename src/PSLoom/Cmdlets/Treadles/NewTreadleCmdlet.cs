// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Treadles;
using PSLoom.Warp;
using PSLoom.Warp.Treadles;

namespace PSLoom.Cmdlets.Treadles;

/// <summary>
///   Defines a treadle: a global function that runs a command with arguments baked in, followed by whatever the caller typed —
///   <c>New-Treadle glog { git log --oneline }</c> makes <c>glog -n 3</c> run <c>git log --oneline -n 3</c>.
/// </summary>
[Cmdlet(VerbsCommon.New, "Treadle")]
[OutputType(typeof(TreadleDefinition))]
public sealed class NewTreadleCmdlet : PSCmdlet {
  /// <summary>
  ///   Gets or sets the treadle name.
  /// </summary>
  [Parameter(Mandatory = true, Position = 0)]
  [ValidateNotNullOrEmpty]
  public string Name { get; set; } = null!;

  /// <summary>
  ///   Gets or sets the command to bake in: a script block or a string. It must be a single command with constant arguments.
  /// </summary>
  /// <remarks>
  ///   Untyped on purpose: PowerShell's binder does not convert an arbitrary string to a <see cref="ScriptBlock" />, so a typed
  ///   parameter would reject the most natural spelling. A script block is used for its text, not invoked, so it captures nothing.
  /// </remarks>
  [Parameter(Mandatory = true, Position = 1)]
  [ValidateNotNull]
  public object Command { get; set; } = null!;

  /// <summary>
  ///   Gets or sets a value indicating whether to replace a command that already has this name.
  /// </summary>
  [Parameter]
  public SwitchParameter Force { get; set; }

  /// <summary>
  ///   Gets or sets a value indicating whether to write the definition to the pipeline.
  /// </summary>
  [Parameter]
  public SwitchParameter PassThru { get; set; }

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      var body = TreadleBody.Parse(Command);
      var outcome = TreadleCatalog.PerRunspace.ForCurrent().Set(this.GetEngine(), Name, body, Force.IsPresent);

      WriteVerbose($"{(outcome.Replaced ? "Replaced" : "Defined")} treadle '{outcome.Name}' as '{body}'.");
      TreadleNarration.ReportSubscribers(this, outcome);

      if (PassThru.IsPresent) {
        WriteObject(outcome.Current);
      }
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }
}
