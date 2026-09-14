// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Warp.Storage;

/// <summary>
///   A harness's own directory under the Creel (<c>&lt;creel&gt;/harnesses/&lt;name&gt;</c>). Encapsulation against accidents,
///   not a security boundary: a harness is full-trust in-process code.
/// </summary>
public interface IHarnessStorage {
  /// <summary>
  ///   Gets the harness root. The directory is created lazily; it may not exist yet.
  /// </summary>
  CreelPath Root { get; }

  /// <summary>
  ///   Resolves a relative path under <see cref="Root" />.
  /// </summary>
  /// <param name="relativePath">The relative path; may come from remote data.</param>
  /// <returns>A path proven to stay under the root.</returns>
  /// <exception cref="CreelPathException">The path is rooted, escapes the root, or crosses a link pointing outside it.</exception>
  CreelPath Resolve(string relativePath)
    => Root.Combine(relativePath);
}
