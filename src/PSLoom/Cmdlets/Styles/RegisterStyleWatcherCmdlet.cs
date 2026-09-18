// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Styles;
using PSLoom.Warp;
using PSLoom.Warp.Styles;

namespace PSLoom.Cmdlets.Styles;

/// <summary>
///   Registers a script that runs when a style changes. The script receives the <see cref="StyleChange" /> as <c>$_</c> and as
///   its first argument.
/// </summary>
[Cmdlet(VerbsLifecycle.Register, "StyleWatcher")]
[OutputType(typeof(StyleWatcherRegistration))]
public sealed class RegisterStyleWatcherCmdlet : PSCmdlet {
  /// <summary>
  ///   Gets or sets the concrete context to watch, or the context pattern with <see cref="Pattern" />.
  /// </summary>
  [Parameter(Mandatory = true, Position = 0)]
  public string Context { get; set; } = null!;

  /// <summary>
  ///   Gets or sets the style name.
  /// </summary>
  [Parameter(Mandatory = true, Position = 1)]
  public string Name { get; set; } = null!;

  /// <summary>
  ///   Gets or sets the script to run.
  /// </summary>
  [Parameter(Mandatory = true, Position = 2)]
  public ScriptBlock Action { get; set; } = null!;

  /// <summary>
  ///   Gets or sets a value indicating whether <see cref="Context" /> is matched against written context patterns (coarser: fires
  ///   on any matching write, even if nothing resolves differently).
  /// </summary>
  [Parameter]
  public SwitchParameter Pattern { get; set; }

  /// <summary>
  ///   Gets or sets a value indicating whether to run the script immediately with the current value — with
  ///   <see cref="Pattern" />, once per stored definition the pattern matches.
  /// </summary>
  [Parameter]
  public SwitchParameter Replay { get; set; }

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      var action = Action;
      var (registration, replays) = StyleStore.PerRunspace.ForCurrent().AddWatcher(Context, Name, Pattern.IsPresent,
        change => action.InvokeWithContext(null, [new PSVariable("_", change)], change), action, Replay.IsPresent);

      if (replays.Count > 0) {
        StyleNarration.ReportWatchers(this, replays);
      }

      WriteObject(registration);
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }
}
