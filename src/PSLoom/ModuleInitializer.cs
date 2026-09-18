// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Runspaces;
using PSLoom.Runtime.Hooks;
using PSLoom.Runtime.Loom;
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

    Kernel.EnsureAttached();
    AttachEngine(runspace);
  }

  /// <summary>
  ///   Captures <c>$ExecutionContext</c> on the importing thread, where scripts can run, because the runspace proxy is unusable
  ///   while the import pipeline executes. Harness hooks and probes need it before any kernel cmdlet runs. Only the hook wiring
  ///   receives it here: the loom session (verb registry and its reflection) is created on first use, not at import.
  /// </summary>
  private static void AttachEngine(Runspace runspace) {
    try {
      var result = ScriptBlock.Create("$ExecutionContext").InvokeReturnAsIs();

      if ((result is PSObject wrapped ? wrapped.BaseObject : result) is EngineIntrinsics engine) {
        HookBus.PerRunspace.For(runspace).Wiring.AttachEngine(engine);
      }
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      // Kernel cmdlets attach the engine later.
    }
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
