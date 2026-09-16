// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Management.Automation.Runspaces;
using PSLoom.Runtime.Diagnostics;
using PSLoom.Warp.Hooks;

namespace PSLoom.Runtime.Hooks;

/// <summary>
///   The per-runspace hook bus: copy-on-write handler snapshots per kind, isolated dispatch, and lazy wiring.
/// </summary>
internal sealed class HookBus : IHookBus {
  private static readonly int _kindCount = Enum.GetValues<HookKind>().Max(kind => (int)kind) + 1;

  private readonly int[] _dispatching = new int[_kindCount];
  private readonly Lock _lock = new();
  private readonly HookRegistration[][] _snapshots = new HookRegistration[_kindCount][];
  private long _sequence;
  private int _sessionStarted;

  /// <summary>
  ///   Initializes a new instance of the <see cref="HookBus" /> class.
  /// </summary>
  /// <param name="runspace">The runspace every invocation belongs to, captured once.</param>
  public HookBus(Runspace runspace) {
    ArgumentNullException.ThrowIfNull(runspace);

    Runspace = runspace;
    Array.Fill(_snapshots, []);
    Wiring = new HookWiring(this);
  }

  /// <summary>
  ///   Gets the bus of each runspace.
  /// </summary>
  public static RunspaceLocal<HookBus> PerRunspace { get; } = new(static runspace => new HookBus(runspace));

  /// <summary>
  ///   Gets the runspace this bus belongs to.
  /// </summary>
  public Runspace Runspace { get; }

  /// <summary>
  ///   Gets the wiring of this bus's kinds to engine and PSReadLine mechanisms.
  /// </summary>
  public HookWiring Wiring { get; }

  /// <summary>
  ///   Gets the log of handler invocations.
  /// </summary>
  public RingBuffer<HookDiagnosticEntry> Diagnostics { get; } = new();

  /// <summary>
  ///   Gets or sets the duration above which an invocation is flagged slow.
  /// </summary>
  internal TimeSpan SlowThreshold { get; set; } = TimeSpan.FromMilliseconds(200);

  /// <inheritdoc />
  public IDisposable Subscribe(HookKind kind, HookHandler handler, string? name = null) {
    ArgumentNullException.ThrowIfNull(handler);

    var registration = Add(kind, invocation => {
      handler(invocation);
      return null;
    }, null, name);

    Wiring.EnsureWired(kind);
    return new SubscriptionHandle(this, registration.Id);
  }

  /// <summary>
  ///   Creates the invoker of a script handler: the invocation is passed as <c>$_</c> and as the first argument.
  /// </summary>
  internal static Func<HookInvocation, object?> ForScript(ScriptBlock action) {
    ArgumentNullException.ThrowIfNull(action);

    return invocation => action.InvokeWithContext(null, [new PSVariable("_", invocation)], invocation);
  }

  /// <summary>
  ///   Adds a registration. A named registration replaces an existing one of the same kind and name (case-insensitive).
  /// </summary>
  internal HookRegistration Add(HookKind kind, Func<HookInvocation, object?> invoke, ScriptBlock? action, string? name) {
    EnsureKnown(kind);
    ArgumentNullException.ThrowIfNull(invoke);

    lock (_lock) {
      var registration = new HookRegistration(kind, name, invoke, action, ++_sequence);
      var current = _snapshots[(int)kind];

      if (name is not null) {
        current = Array.FindAll(current, existing => !string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase));
      }

      Volatile.Write(ref _snapshots[(int)kind], [.. current, registration]);
      return registration;
    }
  }

  /// <summary>
  ///   Registers a kernel handler: dispatched like any other, never listed by <c>Get-Hook</c> nor removed by <c>Unregister-Hook</c>.
  /// </summary>
  internal HookRegistration AddInternal(HookKind kind, Func<HookInvocation, object?> invoke) {
    EnsureKnown(kind);
    ArgumentNullException.ThrowIfNull(invoke);

    lock (_lock) {
      var registration = new HookRegistration(kind, null, invoke, null, ++_sequence, true);
      Volatile.Write(ref _snapshots[(int)kind], [.. _snapshots[(int)kind], registration]);
      return registration;
    }
  }

  /// <summary>
  ///   Removes a registration by identifier.
  /// </summary>
  internal bool Remove(Guid id) {
    lock (_lock) {
      for (var kind = 0; kind < _kindCount; kind++) {
        var current = _snapshots[kind];
        var index = Array.FindIndex(current, registration => registration.Id == id && !registration.IsInternal);

        if (index < 0) {
          continue;
        }

        Volatile.Write(ref _snapshots[kind], [.. current[..index], .. current[(index + 1)..]]);
        return true;
      }

      return false;
    }
  }

  /// <summary>
  ///   Removes a named registration of a kind.
  /// </summary>
  internal bool Remove(HookKind kind, string name) {
    EnsureKnown(kind);

    lock (_lock) {
      var current = _snapshots[(int)kind];
      var remaining = Array.FindAll(current,
        registration => !string.Equals(registration.Name, name, StringComparison.OrdinalIgnoreCase) || registration.IsInternal);

      if (remaining.Length == current.Length) {
        return false;
      }

      Volatile.Write(ref _snapshots[(int)kind], remaining);
      return true;
    }
  }

  /// <summary>
  ///   Gets registrations, optionally filtered, in registration order.
  /// </summary>
  internal IReadOnlyList<HookRegistration> Get(HookKind? kind = null, string? name = null) {
    var registrations = kind is { } only
      ? (uint)only < (uint)_kindCount ? Volatile.Read(ref _snapshots[(int)only]) : []
      : _snapshots.SelectMany(snapshot => snapshot);

    return [
      .. registrations
        .Where(registration => !registration.IsInternal)
        .Where(registration => name is null || string.Equals(registration.Name, name, StringComparison.OrdinalIgnoreCase))
        .OrderBy(registration => registration.Sequence)
    ];
  }

  /// <summary>
  ///   Checks whether any handler is registered for a kind.
  /// </summary>
  internal bool HasRegistrations(HookKind kind)
    => (uint)kind < (uint)_kindCount && Volatile.Read(ref _snapshots[(int)kind]).Length > 0;

  /// <summary>
  ///   Marks a kind as dispatching. A dispatch triggered from inside a handler of the same kind is skipped, not re-entered.
  /// </summary>
  internal bool TryEnterDispatch(HookKind kind)
    => Interlocked.CompareExchange(ref _dispatching[(int)kind], 1, 0) == 0;

  /// <summary>
  ///   Clears the mark set by <see cref="TryEnterDispatch" />.
  /// </summary>
  internal void ExitDispatch(HookKind kind)
    => Volatile.Write(ref _dispatching[(int)kind], 0);

  /// <summary>
  ///   Dispatches a kind without payload. With no handlers this is a bounds and length check: no allocation.
  /// </summary>
  internal void Dispatch(HookKind kind) {
    if ((uint)kind >= (uint)_kindCount) {
      return;
    }

    var registrations = Volatile.Read(ref _snapshots[(int)kind]);

    if (registrations.Length != 0) {
      DispatchCore(kind, registrations, new HookInvocation(kind, Runspace), null);
    }
  }

  /// <summary>
  ///   Dispatches <see cref="HookKind.DirectoryChanged" />.
  /// </summary>
  internal void DispatchDirectoryChanged(LocationChangedEventArgs eventArgs) {
    var registrations = Volatile.Read(ref _snapshots[(int)HookKind.DirectoryChanged]);

    if (registrations.Length != 0) {
      DispatchCore(HookKind.DirectoryChanged, registrations, new DirectoryChangedInvocation(Runspace, eventArgs), null);
    }
  }

  /// <summary>
  ///   Dispatches <see cref="HookKind.PreExecute" />.
  /// </summary>
  internal void DispatchPreExecute(string commandLine, int cursorPosition) {
    var registrations = Volatile.Read(ref _snapshots[(int)HookKind.PreExecute]);

    if (registrations.Length != 0) {
      DispatchCore(HookKind.PreExecute, registrations, new PreExecuteInvocation(Runspace, commandLine, cursorPosition), null);
    }
  }

  /// <summary>
  ///   Dispatches <see cref="HookKind.CommandNotFound" />, stopping at the first handler that resolves the command.
  /// </summary>
  internal void DispatchCommandNotFound(string commandName, CommandLookupEventArgs eventArgs) {
    var registrations = Volatile.Read(ref _snapshots[(int)HookKind.CommandNotFound]);

    if (registrations.Length != 0) {
      DispatchCore(HookKind.CommandNotFound, registrations, new CommandNotFoundInvocation(Runspace, commandName, eventArgs), eventArgs);
    }
  }

  /// <summary>
  ///   Raises <see cref="HookKind.SessionStarting" /> the first time it is called for this runspace; later calls do nothing.
  /// </summary>
  /// <returns>
  ///   The handlers that failed, already recorded. A caller with an error stream — <c>Invoke-Loom</c> — writes them too, so a broken
  ///   profile hook shows at startup instead of only in <c>Trace-Hook</c>.
  /// </returns>
  internal IReadOnlyList<HookDiagnosticEntry> RaiseSessionStarting() {
    if (Interlocked.Exchange(ref _sessionStarted, 1) != 0) {
      return [];
    }

    Wiring.EnsurePromptCurrent();

    var registrations = Volatile.Read(ref _snapshots[(int)HookKind.SessionStarting]);

    if (registrations.Length == 0) {
      return [];
    }

    var failures = new List<HookDiagnosticEntry>();
    DispatchCore(HookKind.SessionStarting, registrations, new HookInvocation(HookKind.SessionStarting, Runspace), null, failures);

    return failures;
  }

  private static void EnsureKnown(HookKind kind) {
    if ((uint)kind >= (uint)_kindCount) {
      throw HookException.UnknownKind(kind);
    }
  }

  private static bool Resolves(CommandLookupEventArgs eventArgs, object? result) {
    if (eventArgs.CommandScriptBlock is null &&
        eventArgs.Command is null &&
        result is Collection<PSObject> { Count: > 0 } output &&
        output[0].BaseObject is ScriptBlock substitute) {
      eventArgs.CommandScriptBlock = substitute;
    }

    if (eventArgs.CommandScriptBlock is null &&
        eventArgs.Command is null) {
      return false;
    }

    eventArgs.StopSearch = true;
    return true;
  }

  private void DispatchCore(HookKind kind, HookRegistration[] registrations, HookInvocation invocation, CommandLookupEventArgs? lookup,
  List<HookDiagnosticEntry>? failures = null) {
    if (!TryEnterDispatch(kind)) {
      return;
    }

    // Engine events can be delivered on a thread without this runspace as its default (e.g. PowerShell.Exiting while the
    // runspace closes). Script handlers need it, and an exception escaping such a delivery terminates the process.
    var previousRunspace = Runspace.DefaultRunspace;
    var swapRunspace = !ReferenceEquals(previousRunspace, Runspace);

    if (swapRunspace) {
      Runspace.DefaultRunspace = Runspace;
    }

    try {
      foreach (var registration in registrations) {
        var result = InvokeOne(registration, invocation, failures);

        if (lookup is not null &&
            Resolves(lookup, result)) {
          break;
        }
      }
    }
    finally {
      if (swapRunspace) {
        Runspace.DefaultRunspace = previousRunspace;
      }

      ExitDispatch(kind);
    }
  }

  private object? InvokeOne(HookRegistration registration, HookInvocation invocation, List<HookDiagnosticEntry>? failures) {
    var started = Stopwatch.GetTimestamp();
    Exception? failure = null;

    try {
      return registration.Invoke(invocation);
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      failure = exception;
      return null;
    }
    finally {
      var elapsed = Stopwatch.GetElapsedTime(started);
      var entry = new HookDiagnosticEntry(registration, elapsed, elapsed > SlowThreshold, failure);
      Diagnostics.Record(entry);

      if (failure is not null) {
        failures?.Add(entry);
      }
    }
  }

  private sealed class SubscriptionHandle(HookBus bus, Guid id) : IDisposable {
    private int _disposed;

    public void Dispose() {
      if (Interlocked.Exchange(ref _disposed, 1) == 0) {
        bus.Remove(id);
      }
    }
  }
}
