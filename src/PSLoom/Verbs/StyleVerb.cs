// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Styles;
using PSLoom.Warp.Dsl;

namespace PSLoom.Verbs;

/// <summary>
///   <c>Style &lt;context&gt; &lt;name&gt; &lt;value&gt;</c> — defines a style value from a draft, like <c>Set-Style</c>.
/// </summary>
[LoomVerb("Style", typeof(DraftScope), Reweave = ReweaveBehavior.Replay)]
public sealed class StyleVerb : LoomVerb, IRevertibleVerb {
  /// <summary>
  ///   Gets or sets the context pattern.
  /// </summary>
  [Parameter(Mandatory = true, Position = 0)]
  [ReweaveKey]
  public string Context { get; set; } = null!;

  /// <summary>
  ///   Gets or sets the style name.
  /// </summary>
  [Parameter(Mandatory = true, Position = 1)]
  [ReweaveKey]
  public string Name { get; set; } = null!;

  /// <summary>
  ///   Gets or sets the value.
  /// </summary>
  [Parameter(Mandatory = true, Position = 2)]
  [AllowNull]
  [AllowEmptyString]
  [AllowEmptyCollection]
  public object? Value { get; set; }

  /// <summary>
  ///   Removes the definition a previous draft wrote.
  /// </summary>
  public void Revert(ReweaveEntry previous) {
    ArgumentNullException.ThrowIfNull(previous);

    if (previous.BoundParameters.GetValueOrDefault(nameof(Context)) is string context &&
        previous.BoundParameters.GetValueOrDefault(nameof(Name)) is string name) {
      StyleStore.PerRunspace.ForCurrent().RemoveCore(context, name);
    }
  }

  /// <inheritdoc />
  protected override void Weave() {
    var outcome = StyleStore.PerRunspace.ForCurrent().SetCore(Context, Name, Value);

    foreach (var watcher in outcome.Watchers) {
      if (watcher.Exception is not null) {
        ReportError(StyleException.WatcherFailed(watcher));
      }
    }
  }
}
