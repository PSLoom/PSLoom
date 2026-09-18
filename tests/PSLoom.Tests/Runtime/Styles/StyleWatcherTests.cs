// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using JetBrains.Annotations;
using PSLoom.Runtime.Styles;
using PSLoom.Warp.Styles;

namespace PSLoom.Tests.Runtime.Styles;

[TestSubject(typeof(StyleStore))]
public sealed class StyleWatcherTests {
  [Fact]
  public void ConcreteWatcher_FiresWhenResolvedValueChanges() {
    var store = new StyleStore();
    var changes = new List<StyleChange>();
    store.Watch("colorway:theme", "enabled", changes.Add);

    store.Set("colorway:*", "enabled", true);

    changes.ShouldHaveSingleItem().ShouldBe(new StyleChange("colorway:theme", "enabled", null, true));
  }

  [Fact]
  public void ConcreteWatcher_BroaderWriteThatDoesNotWin_DoesNotFire() {
    var store = new StyleStore();
    store.Set("colorway:theme", "enabled", false);
    var changes = new List<StyleChange>();
    store.Watch("colorway:theme", "enabled", changes.Add);

    store.Set("colorway:*", "enabled", true);

    changes.ShouldBeEmpty();
  }

  [Fact]
  public void ConcreteWatcher_RedefinitionWithEqualValue_DoesNotFire() {
    var store = new StyleStore();
    store.Set("app:*", "color", "Cyan");
    var changes = new List<StyleChange>();
    store.Watch("app:main", "color", changes.Add);

    store.Set("app:*", "color", "Cyan");

    changes.ShouldBeEmpty();
  }

  [Fact]
  public void ConcreteWatcher_Removal_FiresWithNullNewValue() {
    var store = new StyleStore();
    store.Set("app:*", "color", "Cyan");
    var changes = new List<StyleChange>();
    store.Watch("app:main", "color", changes.Add);

    store.Remove("app:*", "color");

    changes.ShouldHaveSingleItem().ShouldBe(new StyleChange("app:main", "color", "Cyan", null));
  }

  [Fact]
  public void ConcreteWatcher_RemovalRevealingBroaderValue_FiresWithFallback() {
    var store = new StyleStore();
    store.Set("app:*", "color", "Green");
    store.Set("app:main", "color", "Red");
    var changes = new List<StyleChange>();
    store.Watch("app:main", "color", changes.Add);

    store.Remove("app:main", "color");

    changes.ShouldHaveSingleItem().ShouldBe(new StyleChange("app:main", "color", "Red", "Green"));
  }

  [Fact]
  public void ConcreteWatcher_OtherName_DoesNotFire() {
    var store = new StyleStore();
    var changes = new List<StyleChange>();
    store.Watch("app:main", "color", changes.Add);

    store.Set("app:*", "size", 12);

    changes.ShouldBeEmpty();
  }

  [Fact]
  public void PatternWatcher_FiresOnMatchingWriteEvenIfNothingResolvesDifferently() {
    var store = new StyleStore();
    store.Set("colorway:theme:colors:keyword", "fg", "Blue");
    var changes = new List<StyleChange>();
    store.WatchPattern("colorway:*", "fg", changes.Add);

    store.Set("colorway:theme:colors:keyword", "fg", "Blue");
    store.Set("other:*", "fg", "Red");

    changes.ShouldHaveSingleItem().ShouldBe(new StyleChange("colorway:theme:colors:keyword", "fg", "Blue", "Blue"));
  }

  [Fact]
  public void Watchers_RunInRegistrationOrder() {
    var store = new StyleStore();
    var order = new List<string>();
    store.WatchPattern("app:*", "color", _ => order.Add("pattern"));
    store.Watch("app:main", "color", _ => order.Add("concrete"));
    store.WatchPattern("*", "color", _ => order.Add("second-pattern"));

    store.Set("app:*", "color", "Cyan");

    order.ShouldBe(["pattern", "concrete", "second-pattern"]);
  }

  [Fact]
  public void FailingWatcher_DoesNotStopOthers_AndEveryFailureIsReported() {
    var store = new StyleStore();
    var ran = false;
    store.Watch("app:main", "color", _ => throw new InvalidOperationException("first"));
    store.Watch("app:main", "color", _ => ran = true);
    store.Watch("app:main", "color", _ => throw new InvalidOperationException("third"));

    var result = store.Set("app:*", "color", "Cyan");

    ran.ShouldBeTrue();
    result.Applied.ShouldBeTrue();
    result.Succeeded.ShouldBeFalse();
    result.WatcherFailures.Select(failure => failure.Message).ShouldBe(["first", "third"]);
    store.Diagnostics.Snapshot().Count(entry => entry.Exception is not null).ShouldBe(2);
  }

  [Fact]
  public void Watch_WithReplay_DeliversCurrentValue() {
    var store = new StyleStore();
    store.Set("app:*", "color", "Cyan");
    var changes = new List<StyleChange>();

    store.Watch("app:main", "color", changes.Add, true);

    changes.ShouldHaveSingleItem().ShouldBe(new StyleChange("app:main", "color", null, "Cyan"));
  }

  [Fact]
  public void WatchPattern_WithReplay_DeliversEveryMatchingDefinitionInOrder() {
    // Harvested: StyleWatcherDispatchTests.Replay_PatternWatcher_FiresOncePerCurrentlyMatchingDefinition.
    var store = new StyleStore();
    store.Set("colorway:b", "theme", "B");
    store.Set("other:*", "theme", "X");
    store.Set("colorway:a", "theme", "A");
    var changes = new List<StyleChange>();

    store.WatchPattern("colorway:*", "theme", changes.Add, true);

    changes.Select(change => (change.Context, change.OldValue, change.NewValue)).ShouldBe([("colorway:b", null, "B"), ("colorway:a", null, "A")]);
  }

  [Fact]
  public void Watch_WithReplayAndNothingResolved_DoesNotFire() {
    var store = new StyleStore();
    var changes = new List<StyleChange>();

    store.Watch("app:main", "color", changes.Add, true);

    changes.ShouldBeEmpty();
  }

  [Fact]
  public void Watch_ReplayFailure_ThrowsStyleExceptionButKeepsWatcher() {
    var store = new StyleStore();
    store.Set("app:*", "color", "Cyan");
    var calls = 0;

    Should.Throw<StyleException>(() => store.Watch("app:main", "color", _ => {
      calls++;
      throw new InvalidOperationException("boom");
    }, true)).ErrorId.ShouldBe(StyleException.WATCHER_FAILED);

    store.Set("app:*", "color", "Red");
    calls.ShouldBe(2);
  }

  [Fact]
  public void DisposedWatcher_StopsFiring() {
    var store = new StyleStore();
    var changes = new List<StyleChange>();
    var handle = store.Watch("app:main", "color", changes.Add);

    handle.Dispose();
    handle.Dispose();
    store.Set("app:*", "color", "Cyan");

    changes.ShouldBeEmpty();
  }

  [Fact]
  public void Reads_NeverRunWatchers() {
    var store = new StyleStore();
    store.Set("app:*", "color", "Cyan");
    var calls = 0;
    store.Watch("app:main", "color", _ => calls++);
    store.WatchPattern("*", "color", _ => calls++);

    store.Resolve("app:main", "color");
    ((IStyleStore)store).TryGet<string>("app:main", "color", out var _);
    store.GetDefinitions();

    calls.ShouldBe(0);
  }

  [Fact]
  public void WriteFromWatcher_IsAllowed() {
    var store = new StyleStore();
    store.Watch("app:main", "color", change => store.Set("app:*", "mirror", change.NewValue));

    store.Set("app:*", "color", "Cyan");

    store.Resolve("app:main", "mirror")!.Value.ShouldBe("Cyan");
  }

  [Fact]
  public void SelfTriggeringWatchers_StopAtDepthLimit() {
    var store = new StyleStore();
    var writes = 0;
    store.WatchPattern("*", "counter", _ => store.Set("app:*", "counter", ++writes));

    Should.NotThrow(() => store.Set("app:*", "counter", 0));

    writes.ShouldBe(StyleStore.MAX_WATCHER_DEPTH);
    store.Diagnostics.Snapshot().ShouldContain(entry =>
      entry.Exception is StyleException && ((StyleException)entry.Exception).ErrorId == StyleException.WATCHER_RECURSION);
  }
}
