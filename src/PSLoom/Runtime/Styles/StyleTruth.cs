// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Runtime.Styles;

/// <summary>
///   The truthiness rule behind <c>Test-Style</c>.
/// </summary>
internal static class StyleTruth {
  private static readonly string[] _truthyStrings = ["true", "yes", "1", "on", "enabled"];

  /// <summary>
  ///   Checks whether a style value counts as enabled: <see langword="true" />, a non-zero integer, or one of
  ///   <c>true</c>/<c>yes</c>/<c>1</c>/<c>on</c>/<c>enabled</c> (case-insensitive).
  /// </summary>
  public static bool IsTrue(object? value)
    => value switch {
      bool flag => flag,
      string text => Array.Exists(_truthyStrings, truthy => string.Equals(truthy, text.Trim(), StringComparison.OrdinalIgnoreCase)),
      sbyte or byte or short or ushort or int or uint or long or ulong => Convert.ToDecimal(value) != 0,
      var _ => false
    };
}
