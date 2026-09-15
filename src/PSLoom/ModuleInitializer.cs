// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Runspaces;
using PSLoom.Runtime.Hooks;
using PSLoom.Warp.Hooks;

namespace PSLoom;

/// <summary>
///   Subscribes the <see cref="HookKind.SessionExiting" /> hook to the engine's <see cref="PSEngineEvent.Exiting" /> event when the
///   module is imported, and unsubscribes when it is removed. No cmdlet or registration is needed for this kind.
/// </summary>
public sealed class ModuleInitializer : IModuleAssemblyInitializer, IModuleAssemblyCleanup {
  // Engine events are identified by their event name: a custom source identifier subscribes to something the engine never raises.
  internal const string EXITING_SOURCE_IDENTIFIER = PSEngineEvent.Exiting;

  private PSEventSubscriber? _exitingSubscriber;
  private Runspace? _runspace;

  /// <inheritdoc />
  public void OnImport() {
    if (Runspace.DefaultRunspace is not { } runspace) {
      return;
    }

    _runspace = runspace;
    _exitingSubscriber = runspace.Events.SubscribeEvent(null, null, EXITING_SOURCE_IDENTIFIER, null, OnEngineExiting, true, false);
  }

  /// <inheritdoc />
  public void OnRemove(PSModuleInfo module) {
    if (_exitingSubscriber is null) {
      return;
    }

    _runspace?.Events.UnsubscribeEvent(_exitingSubscriber);
    _exitingSubscriber = null;
    _runspace = null;
  }

  internal void OnEngineExiting(object? sender, PSEventArgs eventArgs) {
    try {
      if (_runspace is { } runspace) {
        HookBus.PerRunspace.For(runspace).Dispatch(HookKind.SessionExiting);
      }
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      // Best effort during teardown; nothing can observe a failure here.
    }
  }
}
