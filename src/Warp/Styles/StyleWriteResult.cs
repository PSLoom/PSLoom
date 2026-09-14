namespace PSLoom.Warp.Styles;

/// <summary>
///   The outcome of a write: whether it applied, and every watcher failure. Watchers are isolated; all of them run.
/// </summary>
public sealed class StyleWriteResult {
  internal StyleWriteResult(bool applied, IReadOnlyList<Exception> watcherFailures) {
    Applied = applied;
    WatcherFailures = watcherFailures;
  }

  /// <summary>
  ///   Gets a value indicating whether the store changed.
  /// </summary>
  public bool Applied { get; }

  /// <summary>
  ///   Gets the exceptions thrown by watchers, in execution order.
  /// </summary>
  public IReadOnlyList<Exception> WatcherFailures { get; }

  /// <summary>
  ///   Gets a value indicating whether every watcher succeeded.
  /// </summary>
  public bool Succeeded => WatcherFailures.Count == 0;
}