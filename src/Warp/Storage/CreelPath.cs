// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Storage;

/// <summary>
///   An absolute path proven to lie under a Creel root. The only ways to obtain one are the kernel-created root and
///   <see cref="Combine" />, which canonicalizes and bounds-checks, so a value existing at all means the check already ran.
/// </summary>
public readonly struct CreelPath : IEquatable<CreelPath> {
  private static readonly StringComparison _comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
  private static readonly char[] _separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

  private readonly string? _root;
  private readonly string? _fullPath;

  private CreelPath(string root, string fullPath) {
    _root = root;
    _fullPath = fullPath;
  }

  /// <summary>
  ///   Gets a value indicating whether this is the uninitialized <c>default</c> value.
  /// </summary>
  public bool IsDefault => _fullPath is null;

  /// <summary>
  ///   Gets the absolute path.
  /// </summary>
  public string FullPath => _fullPath ?? throw DefaultValueException();

  /// <summary>
  ///   Gets the path relative to its root (<c>.</c> for the root itself).
  /// </summary>
  public string RelativePath => Path.GetRelativePath(RootPath, FullPath);

  /// <summary>
  ///   Gets the final segment.
  /// </summary>
  public string Name => Path.GetFileName(FullPath);

  /// <summary>
  ///   Gets a value indicating whether this path is its root.
  /// </summary>
  public bool IsRoot => string.Equals(RootPath, FullPath, _comparison);

  /// <summary>
  ///   Gets the parent directory, or <see langword="null" /> at the root (a parent is never allowed to leave it).
  /// </summary>
  public CreelPath? Parent => IsRoot ? null : new CreelPath(RootPath, Path.GetDirectoryName(FullPath)!);

  private string RootPath => _root ?? throw DefaultValueException();

  /// <summary>
  ///   Resolves a relative path under this path.
  /// </summary>
  /// <param name="relativePath">The relative path; may come from remote data. Empty returns this path.</param>
  /// <returns>The resolved path.</returns>
  /// <exception cref="CreelPathException">The input is rooted, invalid, escapes the root, or crosses a link pointing outside it.</exception>
  public CreelPath Combine(string relativePath) {
    ArgumentNullException.ThrowIfNull(relativePath);

    var root = RootPath;

    if (relativePath.Length == 0) {
      return this;
    }

    if (Path.IsPathRooted(relativePath)) {
      throw new CreelPathException(root, relativePath, CreelPathViolation.Rooted);
    }

    if (relativePath.Contains('\0') ||
        (OperatingSystem.IsWindows() && relativePath.Contains(':'))) {
      // ':' on Windows addresses alternate data streams ("file:stream"), which escape per-file checks.
      throw new CreelPathException(root, relativePath, CreelPathViolation.InvalidCharacters);
    }

    string fullPath;

    try {
      fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Join(FullPath, relativePath)));
    }
    catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException) {
      throw new CreelPathException(root, relativePath, CreelPathViolation.InvalidCharacters, exception);
    }

    if (!IsUnder(root, fullPath)) {
      throw new CreelPathException(root, relativePath, CreelPathViolation.Escape);
    }

    EnsureNoEscapingLinks(root, fullPath, relativePath);

    return new CreelPath(root, fullPath);
  }

  /// <summary>
  ///   Resolves a relative path under <paramref name="path" />.
  /// </summary>
  /// <param name="path">The base path.</param>
  /// <param name="relativePath">The relative path.</param>
  /// <returns>The resolved path.</returns>
  public static CreelPath operator /(CreelPath path, string relativePath)
    => path.Combine(relativePath);

  /// <summary>
  ///   Converts to the absolute path string, for use with file APIs.
  /// </summary>
  /// <param name="path">The path.</param>
  public static implicit operator string(CreelPath path)
    => path.FullPath;

  /// <summary>
  ///   Compares two paths using the platform's path comparison.
  /// </summary>
  public static bool operator ==(CreelPath left, CreelPath right)
    => left.Equals(right);

  /// <summary>
  ///   Compares two paths using the platform's path comparison.
  /// </summary>
  public static bool operator !=(CreelPath left, CreelPath right)
    => !left.Equals(right);

  /// <inheritdoc />
  public bool Equals(CreelPath other)
    => string.Equals(_fullPath, other._fullPath, _comparison) && string.Equals(_root, other._root, _comparison);

  /// <inheritdoc />
  public override bool Equals(object? obj)
    => obj is CreelPath other && Equals(other);

  /// <inheritdoc />
  public override int GetHashCode()
    => _fullPath is null ? 0 : string.GetHashCode(_fullPath, _comparison);

  /// <inheritdoc />
  public override string ToString()
    => _fullPath ?? string.Empty;

  /// <summary>
  ///   Creates a root. Kernel only: roots come from the Creel resolver, never from harness input.
  /// </summary>
  internal static CreelPath CreateRoot(string absolutePath) {
    if (!Path.IsPathFullyQualified(absolutePath)) {
      throw new ArgumentException($"A Creel root must be fully qualified, got '{absolutePath}'.", nameof(absolutePath));
    }

    var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(absolutePath));
    return new CreelPath(root, root);
  }

  private static bool IsUnder(string root, string fullPath) {
    if (string.Equals(root, fullPath, _comparison)) {
      return true;
    }

    var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;

    return fullPath.StartsWith(prefix, _comparison);
  }

  private static void EnsureNoEscapingLinks(string root, string fullPath, string relativePath) {
    if (string.Equals(root, fullPath, _comparison)) {
      return;
    }

    var current = root;

    foreach (var segment in Path.GetRelativePath(root, fullPath).Split(_separators, StringSplitOptions.RemoveEmptyEntries)) {
      current = Path.Join(current, segment);

      FileSystemInfo info = new FileInfo(current);

      if (info.LinkTarget is null) {
        // Not a link (or does not exist). Nothing below a missing segment can exist either.
        if (!info.Exists &&
            !Directory.Exists(current)) {
          return;
        }

        continue;
      }

      string? target;

      try {
        target = info.ResolveLinkTarget(true)?.FullName;
      }
      catch (IOException exception) {
        throw new CreelPathException(root, relativePath, CreelPathViolation.LinkEscape, exception);
      }

      if (target is null ||
          !IsUnder(root, Path.TrimEndingDirectorySeparator(target))) {
        throw new CreelPathException(root, relativePath, CreelPathViolation.LinkEscape);
      }
    }
  }

  private static InvalidOperationException DefaultValueException()
    => new("This CreelPath is the default value; obtain one from IHarnessStorage.");
}
