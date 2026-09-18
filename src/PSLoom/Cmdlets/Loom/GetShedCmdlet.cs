// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Loom;
using PSLoom.Runtime.Sheds;
using PSLoom.Warp;
using PSLoom.Warp.Dsl;

namespace PSLoom.Cmdlets.Loom;

/// <summary>
///   Lists the statements the draft staged with <c>Shed</c>: when each applies, where it stands, how long it took and what failed.
/// </summary>
[Cmdlet(VerbsCommon.Get, "Shed")]
[OutputType(typeof(ShedEntry))]
public sealed class GetShedCmdlet : PSCmdlet {
  /// <summary>
  ///   Gets or sets the states to list; every state when omitted.
  /// </summary>
  [Parameter(Position = 0)]
  public ShedState[]? State { get; set; }

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      var entries = LoomSession.PerRunspace.ForCurrent().Sheds.Entries;

      foreach (var entry in entries.Where(entry => State is not { Length: > 0 } || State.Contains(entry.State))) {
        WriteObject(entry);
      }
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }
}
