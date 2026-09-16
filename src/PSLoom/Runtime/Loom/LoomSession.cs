// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Runspaces;
using PSLoom.Runtime.Harnesses;
using PSLoom.Runtime.Hooks;
using PSLoom.Runtime.Sheds;
using PSLoom.Runtime.Verbs;
using PSLoom.Verbs;
using PSLoom.Warp;
using PSLoom.Warp.Dsl;
using PSLoom.Warp.Kernel;

namespace PSLoom.Runtime.Loom;

/// <summary>
///   The loom state of one runspace: verbs, harnesses, the runs executing now, and the last completed draft with its reweave
///   ledger.
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
    Verbs.Add(typeof(TreadleVerb), KERNEL_OWNER);
    Directory = new HarnessDirectory();
    Harnesses = new HarnessRegistry(this);
    Sheds = new ShedStaging();
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
  ///   Gets the statements staged with <c>Shed</c> and their queue.
  /// </summary>
  public ShedStaging Sheds { get; }

  /// <summary>
  ///   Gets the harness names <c>Thread</c> accepts. Starts as the first-party set; tests extend it.
  /// </summary>
  public HashSet<string> FirstParty { get; } = FirstPartyHarnesses.CreateSet();

  /// <summary>
  ///   Gets or sets how module operations are performed for a cmdlet. <see langword="null" /> uses PowerShell itself; tests
  ///   replace it.
  /// </summary>
  internal Func<PSCmdlet, IHarnessModules>? HarnessModulesFactory { get; set; }

  /// <summary>
  ///   Gets or sets the lock serializing installs across sessions.
  /// </summary>
  internal HarnessInstallLock InstallLock {
    get => field ??= HarnessInstallLock.ForCreel();
    set;
  }

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
  ///   Gets the error-free top-level invocations of the last completed draft.
  /// </summary>
  public IReadOnlyList<LedgerItem> Ledger { get; private set; } = [];

  /// <summary>
  ///   Gets the innermost run executing now.
  /// </summary>
  public LoomRun? CurrentRun => _runs.TryPeek(out var run) ? run : null;

  public IHarnessModules ModulesFor(PSCmdlet cmdlet)
    => HarnessModulesFactory?.Invoke(cmdlet) ?? new PowerShellHarnessModules(cmdlet);

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
    Ledger = run.Ledger;
  }

  /// <summary>
  ///   Starts a verb invocation inside the innermost run, checking that the verb is valid in the current scope. A top-level draft
  ///   invocation gets a ledger entry, and is skipped when a reweave finds it unchanged.
  /// </summary>
  public IVerbInvocation BeginVerb(LoomVerb verb) {
    ArgumentNullException.ThrowIfNull(verb);

    var descriptor = Verbs.ForType(verb.GetType());

    if (CurrentRun is not { } run ||
        descriptor is null) {
      throw WarpException.VerbOutsideLoom(descriptor?.Name ?? verb.GetType().Name);
    }

    var invocation = verb.GetVariableValue("MyInvocation") as InvocationInfo;
    var line = invocation?.ScriptLineNumber;

    if (!descriptor.IsValidIn(run.CurrentScope)) {
      run.Report(LoomException.VerbOutOfScope(descriptor.Name, run.CurrentScope, descriptor.Scopes, line).ToErrorRecord());
      return new VerbInvocation(this, run, descriptor, false, line);
    }

    if (!run.IsDraft ||
        run.Active.Count > 0) {
      return new VerbInvocation(this, run, descriptor, true, line);
    }

    var bound = BoundParameters(verb);
    var fingerprint = ReweaveFingerprint.Compute(bound);
    var key = run.DisambiguateKey(ReweaveFingerprint.Key(descriptor.Name, descriptor.ReweaveKeys, bound, fingerprint));
    var entry = new ReweaveEntry(descriptor.Name, key, fingerprint, bound);

    return new VerbInvocation(this, run, descriptor, !run.IsUnchanged(key, fingerprint), line, entry);
  }

  private static Dictionary<string, object?> BoundParameters(LoomVerb verb) {
    var bound = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    foreach (var (name, value) in verb.MyInvocation.BoundParameters) {
      bound[name] = value;
    }

    return bound;
  }
}
