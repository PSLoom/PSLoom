// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Storage;

/// <summary>
///   Raised when a relative path cannot be resolved under a Creel root. Rejects; never clamps.
/// </summary>
public sealed class CreelPathException : PowerShellException {
  internal const string PATH_ESCAPE = "CREEL_PATH_ESCAPE";

  internal CreelPathException(string root, string relativePath, CreelPathViolation violation, Exception? innerException = null)
    : base(PATH_ESCAPE, ErrorCategory.PermissionDenied, BuildMessage(root, relativePath, violation), relativePath, innerException) {
    Root = root;
    RelativePath = relativePath;
    Violation = violation;
  }

  /// <summary>
  ///   Gets the root the path had to stay under.
  /// </summary>
  public string Root { get; }

  /// <summary>
  ///   Gets the rejected input.
  /// </summary>
  public string RelativePath { get; }

  /// <summary>
  ///   Gets the rule the input broke.
  /// </summary>
  public CreelPathViolation Violation { get; }

  private static string BuildMessage(string root, string relativePath, CreelPathViolation violation)
    => violation switch {
      CreelPathViolation.Rooted => $"'{relativePath}' is rooted; only paths relative to '{root}' are accepted.",
      CreelPathViolation.InvalidCharacters => $"'{relativePath}' contains characters that are not allowed in a path.",
      CreelPathViolation.LinkEscape => $"'{relativePath}' crosses a link that points outside '{root}'.",
      _ => $"'{relativePath}' resolves outside '{root}'."
    };
}
