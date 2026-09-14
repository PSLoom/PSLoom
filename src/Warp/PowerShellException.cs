// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp;

/// <summary>
///   Base type for every exception PSLoom and its harnesses raise. Carries what PowerShell needs to report it as an
///   <see cref="ErrorRecord" /> instead of an opaque .NET exception.
/// </summary>
public abstract class PowerShellException : Exception {
  /// <summary>
  ///   Initializes a new instance of the <see cref="PowerShellException" /> class.
  /// </summary>
  /// <param name="errorId">A stable, owner-prefixed SCREAMING_SNAKE identifier (e.g. <c>CREEL_PATH_ESCAPE</c>).</param>
  /// <param name="errorCategory">The PowerShell error category.</param>
  /// <param name="message">The human-readable message.</param>
  /// <param name="targetObject">The object the operation failed on, if any.</param>
  /// <param name="innerException">The underlying exception, if any.</param>
  protected PowerShellException(string errorId, ErrorCategory errorCategory, string message, object? targetObject = null,
  Exception? innerException = null) : base(message, innerException) {
    ArgumentException.ThrowIfNullOrWhiteSpace(errorId);

    ErrorId = errorId;
    ErrorCategory = errorCategory;
    TargetObject = targetObject;
  }

  /// <summary>
  ///   Gets the stable error identifier.
  /// </summary>
  public string ErrorId { get; }

  /// <summary>
  ///   Gets the PowerShell error category.
  /// </summary>
  public ErrorCategory ErrorCategory { get; }

  /// <summary>
  ///   Gets the object the operation failed on, if any.
  /// </summary>
  public object? TargetObject { get; }

  /// <summary>
  ///   Converts this exception to an <see cref="ErrorRecord" /> suitable for <c>WriteError</c>.
  /// </summary>
  /// <returns>A new error record wrapping this exception.</returns>
  public virtual ErrorRecord ToErrorRecord()
    => new(this, ErrorId, ErrorCategory, TargetObject);
}
