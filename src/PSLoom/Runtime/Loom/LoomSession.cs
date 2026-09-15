// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Runspaces;
using PSLoom.Runtime.Harnesses;
using PSLoom.Runtime.Hooks;
using PSLoom.Runtime.Verbs;
using PSLoom.Verbs;
using PSLoom.Warp;
using PSLoom.Warp.Dsl;
using PSLoom.Warp.Kernel;

namespace PSLoom.Runtime.Loom;

/// <summary>
///   The loom state of one runspace: verbs, harnesses, the runs executing now, and the last completed draft.
/// </summary>
internal sealed class LoomSession {
  internal const string KERNEL_OWNER = "PSLoom";

  private readonly Stack<LoomRun> _runs = new();

  public LoomSession(Runspace runspace) {
    ArgumentNullException.ThrowIfNull(runspace);

    Runspace = runspace;
    Verbs = new VerbRegistry();
    Verbs.Add(typeof(StyleVerb), KERNEL_OWNER);
    Verbs.Add(typeof(ThreadVerb), KERNEL_OWNER);
    Directory = new HarnessDirectory();
    Harnesses = new HarnessRegistry(this);
  }

  /// <summary>
  ///   Gets the session of each runspace.
  /// </summary>
  public static RunspaceLocal<LoomSession> PerRunspace { get; } = new(static runspace => new LoomSession(runspace));

  public Runspace Runspace { get; }

  public VerbRegistry Verbs { get; }

  public HarnessRegistry Harnesses { get; }

  public HarnessDirectory Directory { get; }

  /// <summary>
  ///   Gets the engine intrinsics, once any kernel entry point provided them (held by the runspace's hook wiring).
  /// </summary>
  public EngineIntrinsics? Engine => HookBus.PerRunspace.For(Runspace).Wiring.Engine;

  /// <summary>
  ///   Gets a value indicating whether a draft completed in this runspace.
  /// </summary>
  public bool IsWoven { get; private set; }

  /// <summary>
  ///   Gets the timings of the last completed draft.
  /// </summary>
  public IReadOnlyList<LoomTiming>? LastRun { get; private set; }

  /// <summary>
  ///   Gets the innermost run executing now.
  /// </summary>
  public LoomRun? CurrentRun => _runs.TryPeek(out var run) ? run : null;

  /// <summary>
  ///   Provides engine intrinsics to the session and its hook wiring; the first call wins.
  /// </summary>
  public void AttachEngine(EngineIntrinsics engine) {
    ArgumentNullException.ThrowIfNull(engine);
    HookBus.PerRunspace.For(Runspace).Wiring.AttachEngine(engine);
  }

  public void PushRun(LoomRun run)
    => _runs.Push(run);

  public void PopRun(LoomRun run) {
    if (_runs.TryPeek(out var top) &&
        ReferenceEquals(top, run)) {
      _runs.Pop();
    }
  }

  public void MarkWoven(LoomRun run) {
    IsWoven = true;
    LastRun = run.Timings;
  }

  /// <summary>
  ///   Starts a verb invocation inside the innermost run, checking that the verb is valid in the current scope.
  /// </summary>
  public IVerbInvocation BeginVerb(LoomVerb verb) {
    ArgumentNullException.ThrowIfNull(verb);

    var descriptor = Verbs.ForType(verb.GetType());

    if (CurrentRun is not { } run ||
        descriptor is null) {
      throw WarpException.VerbOutsideLoom(descriptor?.Name ?? verb.GetType().Name);
    }

    var line = (verb.GetVariableValue("MyInvocation") as InvocationInfo)?.ScriptLineNumber;
    var inScope = descriptor.IsValidIn(run.CurrentScope);

    if (!inScope) {
      run.Report(LoomException.VerbOutOfScope(descriptor.Name, run.CurrentScope, descriptor.Scopes, line).ToErrorRecord());
    }

    return new VerbInvocation(this, run, descriptor, inScope, line);
  }
}
