// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Warp.Dsl;
using PSLoom.Warp.Hosting;

namespace PSLoom.Fixture;

/// <summary>
///   The scope inside a <c>Box</c> body.
/// </summary>
public abstract class BoxScope : DslScope;

/// <summary>
///   The state of one <c>Box</c> being built.
/// </summary>
public sealed class BoxFrame(string name) : IDslFrame<BoxScope> {
  /// <summary>
  ///   Gets the box name.
  /// </summary>
  public string Name { get; } = name;

  /// <summary>
  ///   Gets the items declared so far.
  /// </summary>
  public List<string> Items { get; } = [];
}

/// <summary>
///   <c>Box &lt;Name&gt; { Item ... }</c> — collects items and stores them as style <c>fixture:box:&lt;Name&gt;</c> / <c>items</c>.
/// </summary>
[LoomVerb("Box", typeof(DraftScope))]
public sealed class BoxVerb : LoomVerb {
  /// <summary>
  ///   Gets or sets the box name.
  /// </summary>
  [Parameter(Mandatory = true, Position = 0)]
  [ReweaveKey]
  public string Name { get; set; } = null!;

  /// <summary>
  ///   Gets or sets the body.
  /// </summary>
  [Parameter(Position = 1)]
  [OpensScope(typeof(BoxScope))]
  public ScriptBlock? Body { get; set; }

  /// <inheritdoc />
  protected override void Weave() {
    var frame = new BoxFrame(Name);
    Loom.RunScoped(frame, Body);
    HarnessHost.Current<FixtureHarness>().Styles.Set($"fixture:box:{Name}", "items", string.Join(",", frame.Items));
  }
}

/// <summary>
///   <c>Item &lt;Name&gt; [-Loud]</c> — adds an item to the enclosing box.
/// </summary>
[LoomVerb("Item", typeof(BoxScope))]
public sealed class ItemVerb : LoomVerb {
  /// <summary>
  ///   Gets or sets the item name.
  /// </summary>
  [Parameter(Mandatory = true, Position = 0)]
  [Alias("N")]
  public string Name { get; set; } = null!;

  /// <summary>
  ///   Gets or sets a value indicating whether to upper-case the item.
  /// </summary>
  [Parameter]
  public SwitchParameter Loud { get; set; }

  /// <inheritdoc />
  protected override void Weave()
    => Loom.RequireFrame<BoxFrame>().Items.Add(Loud ? Name.ToUpperInvariant() : Name);
}

/// <summary>
///   <c>Fail [&lt;Message&gt;]</c> — throws, to exercise error reporting.
/// </summary>
[LoomVerb("Fail", typeof(DraftScope), typeof(BoxScope))]
public sealed class FailVerb : LoomVerb {
  /// <summary>
  ///   Gets or sets the message.
  /// </summary>
  [Parameter(Position = 0)]
  public string? Message { get; set; }

  /// <inheritdoc />
  protected override void Weave()
    => throw new InvalidOperationException(Message ?? "fixture failure");
}
