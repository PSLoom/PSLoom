// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Runtime.Styles;

/// <summary>
///   One recorded watcher run.
/// </summary>
public sealed class StyleDiagnosticEntry {
  internal StyleDiagnosticEntry(StyleWatcherOutcome outcome) {
    Timestamp = DateTimeOffset.UtcNow;
    WatcherId = outcome.Watcher.Id;
    Context = outcome.Change.Context;
    Name = outcome.Change.Name;
    OldValue = outcome.Change.OldValue;
    NewValue = outcome.Change.NewValue;
    Elapsed = outcome.Elapsed;
    Exception = outcome.Exception;
  }

  /// <summary>
  ///   Gets when the watcher ran.
  /// </summary>
  public DateTimeOffset Timestamp { get; }

  /// <summary>
  ///   Gets the watcher registration identifier.
  /// </summary>
  public Guid WatcherId { get; }

  /// <summary>
  ///   Gets the context passed to the watcher.
  /// </summary>
  public string Context { get; }

  /// <summary>
  ///   Gets the style name.
  /// </summary>
  public string Name { get; }

  /// <summary>
  ///   Gets the previous value.
  /// </summary>
  public object? OldValue { get; }

  /// <summary>
  ///   Gets the new value.
  /// </summary>
  public object? NewValue { get; }

  /// <summary>
  ///   Gets how long the watcher ran.
  /// </summary>
  public TimeSpan Elapsed { get; }

  /// <summary>
  ///   Gets the exception the watcher threw, if any.
  /// </summary>
  public Exception? Exception { get; }
}
