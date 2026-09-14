using System.Management.Automation.Runspaces;

namespace PSLoom.Warp.Hooks;

/// <summary>
///   A <see cref="HookKind.CommandNotFound" /> event. A handler resolves the miss by setting
///   <see cref="CommandLookupEventArgs.Command" /> or <see cref="CommandLookupEventArgs.CommandScriptBlock" />.
/// </summary>
public sealed class CommandNotFoundInvocation : HookInvocation {
  internal CommandNotFoundInvocation(Runspace runspace, string commandName, CommandLookupEventArgs eventArgs)
    : base(HookKind.CommandNotFound, runspace) {
    CommandName = commandName;
    EventArgs = eventArgs;
  }

  /// <summary>
  ///   Gets the name that failed to resolve.
  /// </summary>
  public string CommandName { get; }

  /// <summary>
  ///   Gets the engine's event arguments.
  /// </summary>
  public CommandLookupEventArgs EventArgs { get; }
}
