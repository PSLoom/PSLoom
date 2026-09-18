using System.Management.Automation.Runspaces;

namespace PSLoom.Warp.Hooks;

/// <summary>
///   A <see cref="HookKind.DirectoryChanged" /> event.
/// </summary>
public sealed class DirectoryChangedInvocation : HookInvocation {
  internal DirectoryChangedInvocation(Runspace runspace, LocationChangedEventArgs eventArgs)
    : base(HookKind.DirectoryChanged, runspace)
    => EventArgs = eventArgs;

  /// <summary>
  ///   Gets the engine's event arguments (old and new path).
  /// </summary>
  public LocationChangedEventArgs EventArgs { get; }
}
