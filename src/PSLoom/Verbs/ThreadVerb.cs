// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Loom;
using PSLoom.Warp.Dsl;

namespace PSLoom.Verbs;

/// <summary>
///   <c>Thread &lt;Name&gt;</c> — declares that the draft uses the harness from module <c>PSLoom.&lt;Name&gt;</c>. The loom host
///   loads it before the draft runs, so at run time this verb only confirms that happened.
/// </summary>
[LoomVerb("Thread", typeof(DraftScope), Reweave = ReweaveBehavior.Additive)]
public sealed class ThreadVerb : LoomVerb {
  /// <summary>
  ///   Gets or sets the harness name.
  /// </summary>
  [Parameter(Mandatory = true, Position = 0)]
  [ReweaveKey]
  public string Name { get; set; } = null!;

  /// <inheritdoc />
  protected override void Weave() {
    if (!LoomSession.PerRunspace.ForCurrent().Harnesses.TryGetByName(Name, out _)) {
      ReportError(LoomException.ThreadNotPrepared(Name));
    }
  }
}
