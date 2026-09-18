// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Runtime.Styles;

/// <summary>
///   A context pattern compiled once, at write time, into the cheapest matcher that is exact for it.
/// </summary>
internal sealed class ContextMatcher {
  private static readonly char[] _specificityMetacharacters = ['*', '?', '['];
  private static readonly char[] _wildcardMetacharacters = ['*', '?', '[', '`'];

  private readonly MatcherKind _kind;
  private readonly string _literal;
  private readonly WildcardPattern? _wildcard;

  private ContextMatcher(string pattern, MatcherKind kind, string literal, WildcardPattern? wildcard) {
    Pattern = pattern;
    _kind = kind;
    _literal = literal;
    _wildcard = wildcard;

    var metacharacter = pattern.IndexOfAny(_specificityMetacharacters);
    LiteralPrefixLength = metacharacter < 0 ? pattern.Length : metacharacter;
  }

  /// <summary>
  ///   Gets the pattern the matcher was compiled from.
  /// </summary>
  public string Pattern { get; }

  /// <summary>
  ///   Gets the length of the literal (non-wildcard) prefix, the specificity used to rank matching definitions.
  /// </summary>
  public int LiteralPrefixLength { get; }

  /// <summary>
  ///   Compiles a context pattern (PowerShell wildcard syntax, case-sensitive).
  /// </summary>
  public static ContextMatcher Compile(string pattern) {
    ArgumentNullException.ThrowIfNull(pattern);

    var first = pattern.IndexOfAny(_wildcardMetacharacters);

    if (first < 0) {
      return new ContextMatcher(pattern, MatcherKind.Exact, pattern, null);
    }

    if (first == pattern.Length - 1 &&
        pattern[first] == '*') {
      return new ContextMatcher(pattern, MatcherKind.Prefix, pattern[..first], null);
    }

    return new ContextMatcher(pattern, MatcherKind.Wildcard, string.Empty, WildcardPattern.Get(pattern, WildcardOptions.None));
  }

  /// <summary>
  ///   Checks whether a string matches the pattern.
  /// </summary>
  public bool IsMatch(string value)
    => _kind switch {
      MatcherKind.Exact => string.Equals(value, _literal, StringComparison.Ordinal),
      MatcherKind.Prefix => value.StartsWith(_literal, StringComparison.Ordinal),
      var _ => _wildcard!.IsMatch(value)
    };

  private enum MatcherKind {
    Exact,
    Prefix,
    Wildcard
  }
}
