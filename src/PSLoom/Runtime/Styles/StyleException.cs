// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp;

namespace PSLoom.Runtime.Styles;

/// <summary>
///   Raised by the style store.
/// </summary>
public sealed class StyleException : PowerShellException {
  internal const string WATCHER_RECURSION = "STYLE_WATCHER_RECURSION";
  internal const string WATCHER_FAILED = "STYLE_WATCHER_FAILED";
  internal const string INVALID_KEY = "STYLE_INVALID_KEY";

  private StyleException(string errorId, ErrorCategory errorCategory, string message, object? targetObject, Exception? innerException = null)
    : base(errorId, errorCategory, message, targetObject, innerException) { }

  internal static StyleException WatcherRecursion(string context, string name, int depth)
    => new(WATCHER_RECURSION, ErrorCategory.LimitsExceeded,
      $"Writing style '{name}' for '{context}' exceeds the watcher nesting limit of {depth}; watchers are probably triggering each other.",
      $"{context}:{name}");

  internal static StyleException WatcherFailed(StyleWatcherOutcome outcome)
    => new(WATCHER_FAILED, ErrorCategory.NotSpecified,
      $"Style watcher {outcome.Watcher.Id} ('{outcome.Watcher.Name}' on '{outcome.Watcher.Context}') failed: {Unwrap(outcome.Exception!).Message}",
      outcome.Watcher, outcome.Exception);

  internal static StyleException InvalidKey(string parameterName)
    => new(INVALID_KEY, ErrorCategory.InvalidArgument, $"The style {parameterName} must not be empty.", parameterName);

  private static Exception Unwrap(Exception exception)
    => exception is MethodInvocationException { InnerException: { } inner } ? inner : exception;
}
