namespace PSLoom.Warp.Storage;

/// <summary>
///   Why a path was rejected.
/// </summary>
public enum CreelPathViolation {
  /// <summary>The input was rooted (absolute, drive-relative or UNC).</summary>
  Rooted,

  /// <summary>The input contained characters that are invalid or unsafe in a path segment.</summary>
  InvalidCharacters,

  /// <summary>The canonical path lies outside the root.</summary>
  Escape,

  /// <summary>A segment is a symbolic link or junction whose target lies outside the root.</summary>
  LinkEscape
}