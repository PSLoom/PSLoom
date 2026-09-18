// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation.Language;
using PSLoom.Warp;

namespace PSLoom.Runtime.Treadles;

/// <summary>
///   Raised by the treadle catalog.
/// </summary>
public sealed class TreadleException : PowerShellException {
  internal const string BODY_NOT_SINGLE_COMMAND = "TREADLE_BODY_NOT_SINGLE_COMMAND";
  internal const string NAME_COLLISION = "TREADLE_NAME_COLLISION";
  internal const string INVALID_NAME = "TREADLE_INVALID_NAME";
  internal const string SUBSCRIBER_FAILED = "TREADLE_SUBSCRIBER_FAILED";

  private TreadleException(string errorId, ErrorCategory errorCategory, string message, object? targetObject, Exception? innerException = null)
    : base(errorId, errorCategory, message, targetObject, innerException) { }

  internal static TreadleException BodyNotSingleCommand(string reason, IScriptExtent? extent = null)
    => new(BODY_NOT_SINGLE_COMMAND, ErrorCategory.InvalidArgument,
      $"A treadle body must be a single command with constant arguments: {reason}{Position(extent)}", extent?.Text);

  internal static TreadleException InvalidName(string name)
    => new(INVALID_NAME, ErrorCategory.InvalidArgument,
      $"'{name}' is not a usable treadle name; use letters, digits, '-', '_' or '.' with no spaces or wildcards.", name);

  internal static TreadleException NameCollision(string name, string existing)
    => new(NAME_COLLISION, ErrorCategory.ResourceExists, $"'{name}' is already {existing}; use -Force to replace it.", name);

  internal static TreadleException SubscriberFailed(string name, Exception exception)
    => new(SUBSCRIBER_FAILED, ErrorCategory.NotSpecified,
      $"A treadle subscriber failed after '{name}' changed: {Unwrap(exception).Message}", name, exception);

  private static string Position(IScriptExtent? extent)
    => extent is null ? "." : $" (line {extent.StartLineNumber}, column {extent.StartColumnNumber}): '{extent.Text}'.";

  private static Exception Unwrap(Exception exception)
    => exception is MethodInvocationException { InnerException: { } inner } ? inner : exception;
}
