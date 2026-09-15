// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.PSReadLine;
using PSLoom.Warp.Hooks;

namespace PSLoom.Runtime.Hooks;

/// <summary>
///   Connects hook kinds to the engine and PSReadLine the first time a handler registers for them. Every connection chains
///   what was there before, captures the bus (and so the runspace) at wiring time, and is never unwound.
/// </summary>
/// <param name="bus">The bus whose kinds are wired.</param>
internal sealed class HookWiring(HookBus bus) {
  internal const string LINE_ACCEPTED_PARAMETER = "LineAcceptedHandler";

  private const string PROMPT_WRAPPER_SCRIPT =
    """
    [PSLoom.Runtime.Hooks.HookBridge]::PrePrompt()
    if ($null -ne $global:__psloomOriginalPrompt) {
      & $global:__psloomOriginalPrompt
    }
    else {
      "PS $($ExecutionContext.SessionState.Path.CurrentLocation)$('>' * ($NestedPromptLevel + 1)) "
    }
    """;

  private const string INSTALL_PROMPT_SCRIPT =
    """
    param($Original, $Wrapper)
    $global:__psloomOriginalPrompt = $Original
    Set-Item -Path function:global:prompt -Value $Wrapper
    """;

  private const string READ_LINE_ACCEPTED_SCRIPT = "(Get-PSReadLineOption).LineAcceptedHandler";
  private const string SET_LINE_ACCEPTED_SCRIPT = "param($Handler) Set-PSReadLineOption -LineAcceptedHandler $Handler";

  private readonly Lock _lock = new();
  private readonly HashSet<HookKind> _wired = [];
  private EngineIntrinsics? _engine;

  /// <summary>
  ///   Gets a value indicating whether <c>prompt</c> is currently wrapped by this bus.
  /// </summary>
  internal ScriptBlock? PromptWrapper { get; private set; }

  /// <summary>
  ///   Gets the engine intrinsics of the runspace, once provided. The runspace's single source of truth for them.
  /// </summary>
  internal EngineIntrinsics? Engine {
    get {
      lock (_lock) {
        return _engine;
      }
    }
  }

  /// <summary>
  ///   Provides the engine the wiring needs; the first call wins. Wires any kind that gained handlers before an engine was
  ///   available.
  /// </summary>
  public void AttachEngine(EngineIntrinsics engine) {
    ArgumentNullException.ThrowIfNull(engine);

    lock (_lock) {
      if (_engine is not null) {
        return;
      }

      _engine = engine;
    }

    foreach (var kind in Enum.GetValues<HookKind>()) {
      if (bus.HasRegistrations(kind)) {
        EnsureWired(kind);
      }
    }
  }

  /// <summary>
  ///   Checks whether a kind is wired.
  /// </summary>
  public bool IsWired(HookKind kind) {
    lock (_lock) {
      return _wired.Contains(kind);
    }
  }

  /// <summary>
  ///   Wires a kind if it is not wired yet.
  /// </summary>
  /// <returns>A warning to show when the kind could not be wired (it is retried on the next registration); otherwise <see langword="null" />.</returns>
  public string? EnsureWired(HookKind kind) {
    lock (_lock) {
      if (kind == HookKind.SessionExiting ||
          _wired.Contains(kind) ||
          _engine is not { } engine) {
        return null;
      }

      try {
        string? warning = null;

        var wired = kind switch {
          HookKind.DirectoryChanged => WireDirectoryChanged(engine),
          HookKind.CommandNotFound => WireCommandNotFound(engine),
          HookKind.PrePrompt or HookKind.SessionStarting => WirePrompt(engine),
          HookKind.PreExecute => WirePreExecute(engine, out warning),
          HookKind.Idle => WireIdle(engine),
          var _ => false
        };

        if (wired) {
          _wired.Add(kind);
        }

        return warning;
      }
      catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
        return HookException.WiringFailed(kind, exception).Message;
      }
    }
  }

  /// <summary>
  ///   Re-applies the prompt wrapper if something replaced <c>prompt</c> after it was wrapped (e.g. a prompt tool's init script).
  /// </summary>
  public void EnsurePromptCurrent() {
    lock (_lock) {
      if (PromptWrapper is null ||
          _engine is not { } engine) {
        return;
      }

      try {
        var current = CurrentPrompt(engine);

        if (!ReferenceEquals(current, PromptWrapper)) {
          engine.InvokeCommand.InvokeScript(INSTALL_PROMPT_SCRIPT, current, PromptWrapper);
        }
      }
      catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
        // Best effort: the previous wrapper, or the user's prompt, keeps working.
      }
    }
  }

  private static ScriptBlock? CurrentPrompt(EngineIntrinsics engine)
    => (engine.InvokeCommand.GetCommand("prompt", CommandTypes.Function) as FunctionInfo)?.ScriptBlock;

  private bool WireDirectoryChanged(EngineIntrinsics engine) {
    engine.InvokeCommand.LocationChangedAction =
      (EventHandler<LocationChangedEventArgs>?)Delegate.Combine(engine.InvokeCommand.LocationChangedAction,
        new EventHandler<LocationChangedEventArgs>(OnLocationChanged));

    return true;
  }

  private bool WireCommandNotFound(EngineIntrinsics engine) {
    engine.InvokeCommand.CommandNotFoundAction =
      (EventHandler<CommandLookupEventArgs>?)Delegate.Combine(engine.InvokeCommand.CommandNotFoundAction,
        new EventHandler<CommandLookupEventArgs>(OnCommandNotFound));

    return true;
  }

  private bool WirePrompt(EngineIntrinsics engine) {
    if (PromptWrapper is null) {
      PromptWrapper = ScriptBlock.Create(PROMPT_WRAPPER_SCRIPT);
      engine.InvokeCommand.InvokeScript(INSTALL_PROMPT_SCRIPT, CurrentPrompt(engine), PromptWrapper);
    }

    _wired.Add(HookKind.PrePrompt);
    _wired.Add(HookKind.SessionStarting);
    return true;
  }

  private bool WirePreExecute(EngineIntrinsics engine, out string? warning) {
    var probe = new PSReadLineProbe(engine.InvokeCommand);

    if (!probe.HasOptionParameter(LINE_ACCEPTED_PARAMETER)) {
      warning = probe.IsLoaded
        ? "The loaded PSReadLine does not support -LineAcceptedHandler (PSLoom's PSReadLine build is required); PreExecute hooks will not run."
        : "PSReadLine is not loaded; PreExecute hooks will not run. Register a PreExecute hook again after PSReadLine is imported.";
      return false;
    }

    var existing = engine.InvokeCommand.InvokeScript(READ_LINE_ACCEPTED_SCRIPT) is [{ BaseObject: Action<string, int> previous }, ..]
      ? previous
      : null;

    engine.InvokeCommand.InvokeScript(SET_LINE_ACCEPTED_SCRIPT, (Action<string, int>)Handler);
    warning = null;
    return true;

    void Handler(string line, int cursor) {
      try {
        existing?.Invoke(line, cursor);
      }
      catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
        // The chained handler's failure must not cost PSLoom's hooks their turn, nor break line acceptance.
      }

      try {
        bus.DispatchPreExecute(line, cursor);
      }
      catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
        // Bridges called by PSReadLine never throw.
      }
    }
  }

  private bool WireIdle(EngineIntrinsics engine) {
    // Engine events are identified by their event name as the source identifier.
    engine.Events.SubscribeEvent(null, null, PSEngineEvent.OnIdle, null, OnIdle, true, false);
    return true;
  }

  private void OnLocationChanged(object? sender, LocationChangedEventArgs eventArgs) {
    try {
      bus.DispatchDirectoryChanged(eventArgs);
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      // Bridges called by the engine never throw.
    }
  }

  private void OnCommandNotFound(object? sender, CommandLookupEventArgs eventArgs) {
    try {
      bus.DispatchCommandNotFound(eventArgs.CommandName, eventArgs);
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      // Bridges called by the engine never throw.
    }
  }

  private void OnIdle(object? sender, PSEventArgs eventArgs) {
    try {
      EnsurePromptCurrent();
      bus.Dispatch(HookKind.Idle);
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      // Bridges called by the engine never throw.
    }
  }
}
