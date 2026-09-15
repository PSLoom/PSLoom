// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Styles;

namespace PSLoom.Runtime.Styles;

/// <summary>
///   What happened when one watcher ran.
/// </summary>
internal sealed class StyleWatcherOutcome(StyleWatcherRegistration watcher, StyleChange change, TimeSpan elapsed, Exception? exception) {
  public StyleWatcherRegistration Watcher { get; } = watcher;

  public StyleChange Change { get; } = change;

  public TimeSpan Elapsed { get; } = elapsed;

  public Exception? Exception { get; } = exception;
}
