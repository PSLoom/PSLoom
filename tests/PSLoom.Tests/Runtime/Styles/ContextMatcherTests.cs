// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Runtime.Styles;

namespace PSLoom.Tests.Runtime.Styles;

[TestSubject(typeof(ContextMatcher))]
public sealed class ContextMatcherTests {
  [Theory]
  [InlineData("git:work", "git:work", true)]
  [InlineData("git:work", "git:work:repo", false)]
  [InlineData("git:work", "GIT:WORK", false)]
  [InlineData("git:*", "git:", true)]
  [InlineData("git:*", "git:work:repo", true)]
  [InlineData("git:*", "Git:work", false)]
  [InlineData("git:*:repo", "git:work:repo", true)]
  [InlineData("git:*:repo", "git:work:other", false)]
  [InlineData("git:w?rk", "git:work", true)]
  [InlineData("git:[wh]ork", "git:hork", true)]
  [InlineData("*", "anything", true)]
  public void IsMatch_FollowsCaseSensitiveWildcardSemantics(string pattern, string value, bool expected) {
    ContextMatcher.Compile(pattern).IsMatch(value).ShouldBe(expected);
  }

  [Theory]
  [InlineData("git:work:repo", 13)]
  [InlineData("git:work:*", 9)]
  [InlineData("git:*", 4)]
  [InlineData("git:w?rk", 5)]
  [InlineData("*", 0)]
  public void LiteralPrefixLength_IsIndexOfFirstWildcard(string pattern, int expected) {
    ContextMatcher.Compile(pattern).LiteralPrefixLength.ShouldBe(expected);
  }

  [Fact]
  public void EscapedMetacharacter_UsesWildcardSemantics() {
    var matcher = ContextMatcher.Compile("git:`*");

    matcher.IsMatch("git:*").ShouldBeTrue();
    matcher.IsMatch("git:work").ShouldBeFalse();
  }
}
