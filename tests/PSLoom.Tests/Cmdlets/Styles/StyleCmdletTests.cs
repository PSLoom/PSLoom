// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Cmdlets.Styles;
using PSLoom.Runtime.Styles;
using PSLoom.Tests.Utility;
using PSLoom.Warp.Styles;

namespace PSLoom.Tests.Cmdlets.Styles;

[TestSubject(typeof(SetStyleCmdlet))]
[TestSubject(typeof(GetStyleCmdlet))]
[TestSubject(typeof(TestStyleCmdlet))]
[TestSubject(typeof(RemoveStyleCmdlet))]
[TestSubject(typeof(GetStyleDefinitionCmdlet))]
public sealed class StyleCmdletTests {
  [Fact]
  public void Set_ThenGet_ResolvesMatchingContext() {
    using var session = new KernelSession();

    session.Run("Set-Style -Context 'gitprofiles:*' -Name 'email' -Value 'general@x.com'");
    session.Shell.HadErrors.ShouldBeFalse();

    session.Run("Get-Style -Context 'gitprofiles:work:repo' -Name 'email'").ShouldHaveSingleItem().BaseObject.ShouldBe("general@x.com");
  }

  [Fact]
  public void Set_Positional_ArrayValue_RoundTripsAsOneObject() {
    using var session = new KernelSession();

    session.Run("Set-Style 'app:*' 'paths' @('a', 'b')");

    var value = session.Run("Get-Style 'app:main' 'paths'").ShouldHaveSingleItem().BaseObject.ShouldBeOfType<object[]>();
    value.ShouldBe(["a", "b"]);
  }

  [Fact]
  public void Set_Twice_SameContextAndName_SilentlyOverwrites() {
    using var session = new KernelSession();

    session.Run("Set-Style 'gitprofiles:*' 'email' 'old@x.com'");
    session.Run("Set-Style 'gitprofiles:*' 'email' 'new@x.com'");

    session.Shell.HadErrors.ShouldBeFalse();
    session.Run("Get-Style 'gitprofiles:work:repo' 'email'").ShouldHaveSingleItem().BaseObject.ShouldBe("new@x.com");
    session.Run("Get-StyleDefinition").ShouldHaveSingleItem();
  }

  [Fact]
  public void Get_MoreSpecificContextWins_RegardlessOfDefinitionOrder() {
    using var session = new KernelSession();

    session.Run("Set-Style 'gitprofiles:*' 'email' 'general@x.com'");
    session.Run("Set-Style 'gitprofiles:work:*' 'email' 'work@x.com'");

    session.Run("Get-Style 'gitprofiles:work:repo' 'email'").ShouldHaveSingleItem().BaseObject.ShouldBe("work@x.com");
  }

  [Fact]
  public void Get_NoMatchAndNoDefault_ProducesNoOutput() {
    using var session = new KernelSession();

    session.Run("Get-Style 'unrelated:x' 'email'").ShouldBeEmpty();
    session.Shell.HadErrors.ShouldBeFalse();
  }

  [Fact]
  public void Get_NoMatchWithDefault_ReturnsDefault() {
    using var session = new KernelSession();

    session.Run("Get-Style 'unrelated:x' 'email' -Default 'fallback@x.com'").ShouldHaveSingleItem().BaseObject.ShouldBe("fallback@x.com");
  }

  [Theory]
  [InlineData("'true'")]
  [InlineData("'Yes'")]
  [InlineData("'1'")]
  [InlineData("'ON'")]
  [InlineData("'enabled'")]
  [InlineData("$true")]
  [InlineData("1")]
  public void Test_TruthyValue_ReturnsTrue(string value) {
    using var session = new KernelSession();
    session.Run($"Set-Style 'pnpm:*' 'menu-select' {value}");

    session.Run("Test-Style 'pnpm:run' 'menu-select'").ShouldHaveSingleItem().BaseObject.ShouldBe(true);
  }

  [Theory]
  [InlineData("'no'")]
  [InlineData("$false")]
  [InlineData("0")]
  [InlineData("$null")]
  public void Test_NonTruthyValue_ReturnsFalse(string value) {
    using var session = new KernelSession();
    session.Run($"Set-Style 'pnpm:*' 'menu-select' {value}");

    session.Run("Test-Style 'pnpm:run' 'menu-select'").ShouldHaveSingleItem().BaseObject.ShouldBe(false);
  }

  [Fact]
  public void Test_NoMatchingDefinition_ReturnsFalse() {
    using var session = new KernelSession();

    session.Run("Test-Style 'pnpm:run' 'menu-select'").ShouldHaveSingleItem().BaseObject.ShouldBe(false);
    session.Shell.HadErrors.ShouldBeFalse();
  }

  [Fact]
  public void Remove_ExistingDefinition_RemovesIt() {
    using var session = new KernelSession();
    session.Run("Set-Style 'gitprofiles:*' 'email' 'a@x.com'");

    session.Run("Remove-Style 'gitprofiles:*' 'email'");

    session.Shell.HadErrors.ShouldBeFalse();
    session.Streams.Warning.ShouldBeEmpty();
    session.Run("Get-Style 'gitprofiles:work:repo' 'email'").ShouldBeEmpty();
  }

  [Fact]
  public void Remove_UnknownDefinition_WritesWarningAndDoesNotThrow() {
    using var session = new KernelSession();

    session.Run("Remove-Style 'doesnotexist:*' 'email'");

    session.Shell.HadErrors.ShouldBeFalse();
    session.Streams.Warning.ShouldNotBeEmpty();
  }

  [Fact]
  public void Remove_ExactContextPatternOnly_DoesNotRemoveOtherPatternsForSameName() {
    using var session = new KernelSession();
    session.Run("Set-Style 'gitprofiles:*' 'email' 'general@x.com'");

    session.Run("Remove-Style 'gitprofiles:work:*' 'email'");

    session.Streams.Warning.ShouldNotBeEmpty();
    session.Run("Get-Style 'gitprofiles:work:repo' 'email'").ShouldHaveSingleItem().BaseObject.ShouldBe("general@x.com");
  }

  [Fact]
  public void GetStyleDefinition_PipedToRemoveStyle_RemovesEveryDefinition() {
    using var session = new KernelSession();
    session.Run("Set-Style 'a:*' 'x' 1; Set-Style 'b:*' 'y' 2");

    session.Run("Get-StyleDefinition | Remove-Style");

    session.Shell.HadErrors.ShouldBeFalse();
    session.Run("Get-StyleDefinition").ShouldBeEmpty();
  }

  [Fact]
  public void GetStyleDefinition_Filters_ByContextAndName() {
    using var session = new KernelSession();
    session.Run("Set-Style 'gitprofiles:*' 'email' 'a@x.com'; Set-Style 'pnpm:*' 'registry' 'r'; Set-Style 'gitprofiles:*' 'name' 'Bruno'");

    session.Run("Get-StyleDefinition").Count.ShouldBe(3);
    session.Run("Get-StyleDefinition -Context 'gitprofiles:*'").Count.ShouldBe(2);
    session.Run("Get-StyleDefinition -Name 'name'").ShouldHaveSingleItem().BaseObject.ShouldBeOfType<StyleDefinition>().Value.ShouldBe("Bruno");
  }

  [Fact]
  public void Get_NothingRegistered_ReturnsEmpty() {
    using var session = new KernelSession();

    session.Run("Get-StyleDefinition").ShouldBeEmpty();
  }

  [Fact]
  public void Set_EmptyName_WritesNonTerminatingError() {
    using var session = new KernelSession();

    session.Run("Set-Style 'app:*' '' 1; 'after'").ShouldHaveSingleItem().BaseObject.ShouldBe("after");

    session.Streams.Error.ShouldNotBeEmpty();
  }

  [Fact]
  public void Set_Verbose_NarratesWriteAndWatchers() {
    using var session = new KernelSession();
    session.Run("Set-Style 'app:*' 'color' 'Green'");
    session.Run("Register-StyleWatcher 'app:main' 'color' { } | Out-Null");

    session.Run("Set-Style 'app:*' 'color' 'Cyan' -Verbose");

    var messages = session.Streams.Verbose.Select(record => record.Message).ToArray();
    messages.ShouldContain(message => message.Contains("'Green' -> 'Cyan'") && message.Contains("'color'"));
    messages.ShouldContain(message => message.StartsWith("Watcher ") && message.Contains("ok"));
  }

  [Fact]
  public void Store_IsPerRunspace() {
    using var first = new KernelSession();
    using var second = new KernelSession();

    first.Run("Set-Style 'app:*' 'color' 'Cyan'");

    second.Run("Get-Style 'app:main' 'color'").ShouldBeEmpty();
    StyleStore.PerRunspace.For(first.Runspace).Resolve("app:main", "color").ShouldNotBeNull();
  }
}
