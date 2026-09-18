// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp;
using PSLoom.Warp.Hooks;

namespace PSLoom.Runtime.Hooks;

/// <summary>
///   Raised by the hook bus.
/// </summary>
public sealed class HookException : PowerShellException {
  internal const string WIRING_FAILED = "HOOK_WIRING_FAILED";
  internal const string UNKNOWN_KIND = "HOOK_UNKNOWN_KIND";
  internal const string HANDLER_FAILED = "HOOK_HANDLER_FAILED";

  private HookException(string errorId, ErrorCategory errorCategory, string message, object? targetObject, Exception? innerException = null)
    : base(errorId, errorCategory, message, targetObject, innerException) { }

  internal static HookException WiringFailed(HookKind kind, Exception innerException)
    => new(WIRING_FAILED, ErrorCategory.InvalidOperation, $"Failed to wire the {kind} hook: {innerException.Message}", kind, innerException);

  internal static HookException HandlerFailed(HookDiagnosticEntry entry)
    => new(HANDLER_FAILED, ErrorCategory.NotSpecified,
      $"The {entry.Kind} hook{(entry.Name is null ? string.Empty : $" '{entry.Name}'")} failed: {Unwrap(entry.Exception!).Message}", entry,
      entry.Exception);

  private static Exception Unwrap(Exception exception)
    => exception is MethodInvocationException { InnerException: { } inner } ? inner : exception;

  internal static HookException UnknownKind(HookKind kind)
    => new(UNKNOWN_KIND, ErrorCategory.InvalidArgument, $"'{kind}' is not a hook kind.", kind);
}
