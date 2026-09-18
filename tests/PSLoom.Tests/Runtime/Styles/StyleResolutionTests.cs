// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Management.Automation;
using JetBrains.Annotations;
using PSLoom.Runtime.Styles;
using PSLoom.Warp.Styles;

namespace PSLoom.Tests.Runtime.Styles;

[TestSubject(typeof(StyleStore))]
public sealed class StyleResolutionTests {
  [Fact]
  public void Resolve_NoDefinitionMatches_ReturnsNull() {
    var store = new StyleStore();
    store.Set("other:*", "email", "a@x.com");

    store.Resolve("gitprofiles:work:repo", "email").ShouldBeNull();
  }

  [Fact]
  public void Resolve_OnlyOneMatchingPattern_ReturnsIt() {
    var store = new StyleStore();
    store.Set("gitprofiles:*", "email", "a@x.com");

    store.Resolve("gitprofiles:work:repo", "email")!.Value.ShouldBe("a@x.com");
  }

  [Fact]
  public void Resolve_LongerLiteralPrefixWins_RegardlessOfRegistrationOrder() {
    var store = new StyleStore();
    store.Set("gitprofiles:work:*", "email", "work@x.com");
    store.Set("gitprofiles:*", "email", "general@x.com");

    store.Resolve("gitprofiles:work:repo", "email")!.Value.ShouldBe("work@x.com");
  }

  [Fact]
  public void Resolve_LongerLiteralPrefixWins_EvenWhenDefinedFirst() {
    var store = new StyleStore();
    store.Set("gitprofiles:*", "email", "general@x.com");
    store.Set("gitprofiles:work:*", "email", "work@x.com");

    store.Resolve("gitprofiles:work:repo", "email")!.Value.ShouldBe("work@x.com");
  }

  [Fact]
  public void Resolve_FullyLiteralPattern_AlwaysWinsOverAnyWildcardPattern() {
    var store = new StyleStore();
    store.Set("gitprofiles:work:repo", "email", "exact@x.com");
    store.Set("gitprofiles:work:*", "email", "work@x.com");

    store.Resolve("gitprofiles:work:repo", "email")!.Value.ShouldBe("exact@x.com");
  }

  [Fact]
  public void Resolve_EqualSpecificity_MostRecentSequenceWins() {
    var store = new StyleStore();
    store.Set("gitprofiles:w*", "email", "old@x.com");
    store.Set("gitprofiles:w?rk:*", "email", "new@x.com");

    store.Resolve("gitprofiles:work:repo", "email")!.Value.ShouldBe("new@x.com");
  }

  [Fact]
  public void Resolve_MatchingContextButDifferentName_IsIgnored() {
    var store = new StyleStore();
    store.Set("gitprofiles:*", "name", "Bruno");

    store.Resolve("gitprofiles:work:repo", "email").ShouldBeNull();
  }

  [Fact]
  public void Set_SameContextTwice_OverwritesAndBumpsSequence() {
    var store = new StyleStore();
    store.Set("gitprofiles:*", "email", "old@x.com");
    var first = store.Resolve("gitprofiles:work", "email")!;

    store.Set("gitprofiles:*", "email", "new@x.com");
    var second = store.Resolve("gitprofiles:work", "email")!;

    second.Value.ShouldBe("new@x.com");
    second.Sequence.ShouldBeGreaterThan(first.Sequence);
    store.GetDefinitions().ShouldHaveSingleItem();
  }

  [Fact]
  public void Resolve_AfterWriteToSameName_SeesNewWinnerDespiteCache() {
    var store = new StyleStore();
    store.Set("app:*", "color", "Green");
    store.Resolve("app:main", "color")!.Value.ShouldBe("Green");

    store.Set("app:main", "color", "Red");

    store.Resolve("app:main", "color")!.Value.ShouldBe("Red");
  }

  [Fact]
  public void Resolve_AfterRemoval_FallsBackToNextWinner() {
    var store = new StyleStore();
    store.Set("app:*", "color", "Green");
    store.Set("app:main", "color", "Red");
    store.Resolve("app:main", "color")!.Value.ShouldBe("Red");

    store.Remove("app:main", "color").Applied.ShouldBeTrue();

    store.Resolve("app:main", "color")!.Value.ShouldBe("Green");
  }

  [Fact]
  public void Remove_UnknownDefinition_IsNotApplied() {
    var store = new StyleStore();
    store.Set("app:*", "color", "Green");

    store.Remove("app:main:*", "color").Applied.ShouldBeFalse();
    store.Remove("app:*", "size").Applied.ShouldBeFalse();
  }

  [Fact]
  public void TryGet_ConvertsWithPowerShellRules() {
    IStyleStore store = new StyleStore();
    store.Set("app:*", "retries", "3");

    store.TryGet<int>("app:main", "retries", out var retries).ShouldBeTrue();
    retries.ShouldBe(3);
    store.TryGet<int>("other", "retries", out var _).ShouldBeFalse();
  }

  [Fact]
  public void Set_PSObjectValue_IsUnwrapped() {
    var store = new StyleStore();

    store.Set("app:*", "color", PSObject.AsPSObject("Cyan"));

    store.Resolve("app:main", "color")!.Value.ShouldBeOfType<string>();
  }

  [Fact]
  public void Set_PSCustomObjectValue_IsKeptAsPSObject() {
    var store = new StyleStore();
    var custom = new PSObject();
    custom.Properties.Add(new PSNoteProperty("Fg", "Cyan"));

    store.Set("app:*", "theme", custom);

    store.Resolve("app:main", "theme")!.Value.ShouldBeSameAs(custom);
  }

  [Fact]
  public void Set_EmptyContextOrName_ThrowsStyleException() {
    var store = new StyleStore();

    Should.Throw<StyleException>(() => store.Set("", "color", 1)).ErrorId.ShouldBe(StyleException.INVALID_KEY);
    Should.Throw<StyleException>(() => store.Set("app:*", "", 1)).ErrorId.ShouldBe(StyleException.INVALID_KEY);
  }

  [Fact]
  public void GetDefinitions_FiltersByExactContextAndName_OrderedBySequence() {
    var store = new StyleStore();
    store.Set("git:*", "email", "a");
    store.Set("pnpm:*", "registry", "b");
    store.Set("git:*", "name", "c");

    store.GetDefinitions().Select(definition => definition.Value).ShouldBe(["a", "b", "c"]);
    store.GetDefinitions("git:*").Select(definition => definition.Name).ShouldBe(["email", "name"]);
    store.GetDefinitions(name: "registry").ShouldHaveSingleItem().Context.ShouldBe("pnpm:*");
    store.GetDefinitions("git:*", "name").ShouldHaveSingleItem().Value.ShouldBe("c");
  }

  [Fact]
  public void Resolve_CacheHit_DoesNotAllocate() {
    var store = new StyleStore();
    store.Set("app:*", "color", "Green");
    store.Set("app:main:*", "color", "Red");
    const string CONTEXT = "app:main:window";
    store.Resolve(CONTEXT, "color");

    var before = GC.GetAllocatedBytesForCurrentThread();

    for (var iteration = 0; iteration < 1_000; iteration++) {
      store.Resolve(CONTEXT, "color");
    }

    (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBe(0);
  }
}
