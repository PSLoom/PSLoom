// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Styles;

namespace PSLoom.Runtime.Styles;

/// <summary>
///   A registered style watcher.
/// </summary>
public sealed class StyleWatcherRegistration {
  internal StyleWatcherRegistration(string context, string name, bool isPattern, StyleWatcher callback, ScriptBlock? action, long sequence) {
    Id = Guid.NewGuid();
    Context = context;
    Name = name;
    IsPattern = isPattern;
    Callback = callback;
    Action = action;
    Sequence = sequence;
    RegisteredAt = DateTimeOffset.UtcNow;
    PatternMatcher = isPattern ? ContextMatcher.Compile(context) : null;
  }

  /// <summary>
  ///   Gets the registration identifier.
  /// </summary>
  public Guid Id { get; }

  /// <summary>
  ///   Gets the watched concrete context, or the context pattern for a pattern watcher.
  /// </summary>
  public string Context { get; }

  /// <summary>
  ///   Gets the watched style name.
  /// </summary>
  public string Name { get; }

  /// <summary>
  ///   Gets a value indicating whether this watcher matches written context patterns instead of a resolved context.
  /// </summary>
  public bool IsPattern { get; }

  /// <summary>
  ///   Gets the script action, when registered through <c>Register-StyleWatcher</c>.
  /// </summary>
  public ScriptBlock? Action { get; }

  /// <summary>
  ///   Gets when the watcher was registered.
  /// </summary>
  public DateTimeOffset RegisteredAt { get; }

  internal StyleWatcher Callback { get; }

  internal long Sequence { get; }

  internal ContextMatcher? PatternMatcher { get; }
}
