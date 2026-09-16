// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Cmdlets;
using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Treadles;
using PSLoom.Warp.Dsl;

namespace PSLoom.Verbs;

/// <summary>
///   <c>Treadle &lt;name&gt; { &lt;command&gt; }</c> — defines a name that runs a command with arguments baked in, like
///   <c>New-Treadle</c>. The body is a command template, not a nested scope.
/// </summary>
[LoomVerb("Treadle", typeof(DraftScope), Reweave = ReweaveBehavior.Replay)]
public sealed class TreadleVerb : LoomVerb, IRevertibleVerb {
  /// <summary>
  ///   Gets or sets the treadle name.
  /// </summary>
  [Parameter(Mandatory = true, Position = 0)]
  [ReweaveKey]
  public string Name { get; set; } = null!;

  /// <summary>
  ///   Gets or sets the command to bake in.
  /// </summary>
  [Parameter(Mandatory = true, Position = 1)]
  public ScriptBlock Body { get; set; } = null!;

  /// <summary>
  ///   Removes the treadle a previous draft defined.
  /// </summary>
  public void Revert(ReweaveEntry previous) {
    ArgumentNullException.ThrowIfNull(previous);

    // Revert runs on a fresh instance outside a pipeline, so the engine comes from the session, not from this cmdlet.
    if (previous.BoundParameters.GetValueOrDefault(nameof(Name)) is string name &&
        LoomSession.PerRunspace.ForCurrent().Engine is { } engine) {
      TreadleCatalog.PerRunspace.ForCurrent().Remove(engine, name);
    }
  }

  /// <inheritdoc />
  protected override void Weave() {
    // A draft replaces the treadle it already owns, but shadowing someone else's command is still reported.
    var outcome = TreadleCatalog.PerRunspace.ForCurrent().Set(this.GetEngine(), Name, TreadleBody.Parse(Body), false);

    foreach (var failure in outcome.SubscriberFailures) {
      ReportError(TreadleException.SubscriberFailed(outcome.Name, failure));
    }
  }
}
