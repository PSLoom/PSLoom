// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Hooks;
using PSLoom.Warp.Hosting;

namespace PSLoom.Fixture;

/// <summary>
///   A harness exercising the contract. It reports what happened through styles under <c>fixture:</c>, so tests observe it
///   without referencing this assembly.
/// </summary>
[Harness("Fixture", Description = "Test harness")]
public sealed class FixtureHarness : IHarness {
  /// <inheritdoc />
  public void Compose(IHarnessBuilder builder) {
    builder.Verbs.Add<BoxVerb>();
    builder.Verbs.Add<ItemVerb>();
    builder.Verbs.Add<FailVerb>();

    builder.Styles.Set("fixture:identity", "module", builder.Identity.ModuleName);
    builder.Styles.Set("fixture:identity", "storage", builder.Storage.Root.FullPath);

    var styles = builder.Styles;
    builder.Treadles.Changed += (_, change)
      => styles.Set("fixture:treadles", "last", change.Current is null ? $"<removed {change.Name}>" : change.Name);

    builder.Hooks.Subscribe(HookKind.SessionStarting, _ => {
      styles.TryGet<int>("fixture:events", "session-starting", out var count);
      styles.Set("fixture:events", "session-starting", count + 1);
    }, "fixture-session-starting");
  }
}
