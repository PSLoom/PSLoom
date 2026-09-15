// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Collections.Concurrent;
using System.Diagnostics;
using PSLoom.Runtime.Diagnostics;
using PSLoom.Warp.Styles;

namespace PSLoom.Runtime.Styles;

/// <summary>
///   The per-runspace style store: lock-free resolution, serialized writes, and watchers that run after a write commits.
/// </summary>
internal sealed class StyleStore : IStyleStore {
  internal const int MAX_WATCHER_DEPTH = 16;

  [ThreadStatic]
  private static int _watcherDepth;

  private readonly ConcurrentDictionary<string, StyleBucket> _buckets = new(StringComparer.Ordinal);
  private readonly Dictionary<Guid, StyleWatcherRegistration> _watchersById = [];
  private readonly ConcurrentDictionary<string, StyleWatcherRegistration[]> _watchersByName = new(StringComparer.Ordinal);
  private readonly Lock _writeLock = new();
  private long _sequence;

  /// <summary>
  ///   Gets the store of each runspace.
  /// </summary>
  public static RunspaceLocal<StyleStore> PerRunspace { get; } = new(static _ => new StyleStore());

  /// <summary>
  ///   Gets the log of watcher runs.
  /// </summary>
  public RingBuffer<StyleDiagnosticEntry> Diagnostics { get; } = new();

  /// <inheritdoc />
  public StyleDefinition? Resolve(string context, string name) {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(name);

    return _buckets.TryGetValue(name, out var bucket) ? bucket.Resolve(context) : null;
  }

  /// <inheritdoc />
  public StyleWriteResult Set(string context, string name, object? value)
    => SetCore(context, name, value).ToResult();

  /// <inheritdoc />
  public StyleWriteResult Remove(string context, string name)
    => RemoveCore(context, name).ToResult();

  /// <inheritdoc />
  public IDisposable Watch(string context, string name, StyleWatcher watcher, bool replay = false) {
    var (registration, replayOutcome) = AddWatcher(context, name, false, watcher, null, replay);

    return replayOutcome?.Exception is null
      ? new WatcherHandle(this, registration.Id)
      : throw StyleException.WatcherFailed(replayOutcome);
  }

  /// <inheritdoc />
  public IDisposable WatchPattern(string contextPattern, string name, StyleWatcher watcher)
    => new WatcherHandle(this, AddWatcher(contextPattern, name, true, watcher, null, false).Registration.Id);

  /// <summary>
  ///   Defines or redefines a value and runs the watchers it affects.
  /// </summary>
  internal StyleWriteOutcome SetCore(string context, string name, object? value) {
    ValidateKey(context, name);
    EnsureDepth(context, name);

    var matcher = ContextMatcher.Compile(context);
    value = Unwrap(value);

    StyleDefinition? previous;
    StyleDefinition current;
    PendingFire[] fires;

    lock (_writeLock) {
      var bucket = _buckets.GetOrAdd(name, static _ => new StyleBucket());
      var watchers = WatchersFor(name);
      var before = CaptureConcrete(bucket, watchers);

      previous = bucket.Find(context);
      current = new StyleDefinition(context, name, value, ++_sequence);
      bucket.Set(new StyleEntry(current, matcher));

      fires = CollectFires(bucket, watchers, before, context, name, previous?.Value, value);
    }

    return new StyleWriteOutcome(true, previous, current, RunFires(fires));
  }

  /// <summary>
  ///   Removes a definition and runs the watchers it affects.
  /// </summary>
  internal StyleWriteOutcome RemoveCore(string context, string name) {
    ValidateKey(context, name);
    EnsureDepth(context, name);

    StyleDefinition? removed;
    PendingFire[] fires;

    lock (_writeLock) {
      if (!_buckets.TryGetValue(name, out var bucket)) {
        return new StyleWriteOutcome(false, null, null, []);
      }

      var watchers = WatchersFor(name);
      var before = CaptureConcrete(bucket, watchers);

      removed = bucket.Remove(context);

      if (removed is null) {
        return new StyleWriteOutcome(false, null, null, []);
      }

      fires = CollectFires(bucket, watchers, before, context, name, removed.Value, null);
    }

    return new StyleWriteOutcome(true, removed, null, RunFires(fires));
  }

  /// <summary>
  ///   Registers a watcher, optionally replaying the current value.
  /// </summary>
  internal (StyleWatcherRegistration Registration, StyleWatcherOutcome? Replay) AddWatcher(string context, string name, bool isPattern,
  StyleWatcher callback, ScriptBlock? action, bool replay) {
    ValidateKey(context, name);
    ArgumentNullException.ThrowIfNull(callback);

    StyleWatcherRegistration registration;

    lock (_writeLock) {
      registration = new StyleWatcherRegistration(context, name, isPattern, callback, action, ++_sequence);
      _watchersById[registration.Id] = registration;
      _watchersByName[name] = [.. WatchersFor(name), registration];
    }

    if (!replay ||
        isPattern ||
        Resolve(context, name) is not { } resolved) {
      return (registration, null);
    }

    var outcome = RunFires([new PendingFire(registration, new StyleChange(context, name, null, resolved.Value))]);
    return (registration, outcome[0]);
  }

  /// <summary>
  ///   Unregisters a watcher.
  /// </summary>
  internal bool RemoveWatcher(Guid id) {
    lock (_writeLock) {
      if (!_watchersById.Remove(id, out var registration)) {
        return false;
      }

      _watchersByName[registration.Name] = Array.FindAll(WatchersFor(registration.Name), watcher => watcher.Id != id);
      return true;
    }
  }

  /// <summary>
  ///   Gets every watcher in registration order.
  /// </summary>
  internal IReadOnlyList<StyleWatcherRegistration> GetWatchers() {
    lock (_writeLock) {
      return [.. _watchersById.Values.OrderBy(watcher => watcher.Sequence)];
    }
  }

  /// <summary>
  ///   Gets stored definitions filtered by exact context pattern and/or name, ordered by sequence.
  /// </summary>
  internal IReadOnlyList<StyleDefinition> GetDefinitions(string? context = null, string? name = null) {
    var definitions = name is null
      ? _buckets.Values.SelectMany(bucket => bucket.Definitions)
      : _buckets.TryGetValue(name, out var bucket)
        ? bucket.Definitions
        : [];

    return [
      .. definitions
        .Where(definition => context is null || string.Equals(definition.Context, context, StringComparison.Ordinal))
        .OrderBy(definition => definition.Sequence)
    ];
  }

  internal static object? Unwrap(object? value)
    => value is PSObject { BaseObject: var baseObject } && baseObject is not PSCustomObject ? baseObject : value;

  private static void ValidateKey(string context, string name) {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(name);

    if (context.Length == 0) {
      throw StyleException.InvalidKey("context");
    }

    if (name.Length == 0) {
      throw StyleException.InvalidKey("name");
    }
  }

  private static void EnsureDepth(string context, string name) {
    if (_watcherDepth >= MAX_WATCHER_DEPTH) {
      throw StyleException.WatcherRecursion(context, name, MAX_WATCHER_DEPTH);
    }
  }

  private static StyleDefinition?[] CaptureConcrete(StyleBucket bucket, StyleWatcherRegistration[] watchers) {
    if (watchers.Length == 0) {
      return [];
    }

    var before = new StyleDefinition?[watchers.Length];

    for (var index = 0; index < watchers.Length; index++) {
      if (!watchers[index].IsPattern) {
        before[index] = bucket.Resolve(watchers[index].Context);
      }
    }

    return before;
  }

  private static PendingFire[] CollectFires(StyleBucket bucket, StyleWatcherRegistration[] watchers, StyleDefinition?[] before,
  string writtenContext, string name, object? previousValue, object? writtenValue) {
    if (watchers.Length == 0) {
      return [];
    }

    var fires = new List<PendingFire>(watchers.Length);

    for (var index = 0; index < watchers.Length; index++) {
      var watcher = watchers[index];

      if (watcher.IsPattern) {
        if (watcher.PatternMatcher!.IsMatch(writtenContext)) {
          fires.Add(new PendingFire(watcher, new StyleChange(writtenContext, name, previousValue, writtenValue)));
        }

        continue;
      }

      var old = before[index];
      var now = bucket.Resolve(watcher.Context);

      if (old is null != now is null ||
          !Equals(old?.Value, now?.Value)) {
        fires.Add(new PendingFire(watcher, new StyleChange(watcher.Context, name, old?.Value, now?.Value)));
      }
    }

    return [.. fires];
  }

  private StyleWatcherRegistration[] WatchersFor(string name)
    => _watchersByName.TryGetValue(name, out var watchers) ? watchers : [];

  private StyleWatcherOutcome[] RunFires(PendingFire[] fires) {
    if (fires.Length == 0) {
      return [];
    }

    var outcomes = new StyleWatcherOutcome[fires.Length];

    for (var index = 0; index < fires.Length; index++) {
      var fire = fires[index];
      var started = Stopwatch.GetTimestamp();
      Exception? failure = null;

      _watcherDepth++;

      try {
        fire.Watcher.Callback(fire.Change);
      }
      catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
        failure = exception;
      }
      finally {
        _watcherDepth--;
      }

      outcomes[index] = new StyleWatcherOutcome(fire.Watcher, fire.Change, Stopwatch.GetElapsedTime(started), failure);
      Diagnostics.Record(new StyleDiagnosticEntry(outcomes[index]));
    }

    return outcomes;
  }

  private readonly record struct PendingFire(StyleWatcherRegistration Watcher, StyleChange Change);

  private sealed class WatcherHandle(StyleStore store, Guid id) : IDisposable {
    private int _disposed;

    public void Dispose() {
      if (Interlocked.Exchange(ref _disposed, 1) == 0) {
        store.RemoveWatcher(id);
      }
    }
  }
}
