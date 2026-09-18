// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Treadles;
using PSLoom.Warp;
using PSLoom.Warp.Treadles;

namespace PSLoom.Cmdlets.Treadles;

/// <summary>
///   Lists the treadles defined in this session, or those matching the names given.
/// </summary>
[Cmdlet(VerbsCommon.Get, "Treadle")]
[OutputType(typeof(TreadleDefinition))]
public sealed class GetTreadleCmdlet : PSCmdlet {
  /// <summary>
  ///   Gets or sets the names to list; wildcards are accepted.
  /// </summary>
  [Parameter(Position = 0)]
  [SupportsWildcards]
  public string[]? Name { get; set; }

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      var catalog = TreadleCatalog.PerRunspace.ForCurrent();

      if (Name is not { Length: > 0 }) {
        foreach (var definition in catalog.All.OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)) {
          WriteObject(definition);
        }

        return;
      }

      foreach (var pattern in Name) {
        if (!WildcardPattern.ContainsWildcardCharacters(pattern)) {
          if (catalog.TryGet(pattern, out var definition)) {
            WriteObject(definition);
          }

          continue;
        }

        var wildcard = new WildcardPattern(pattern, WildcardOptions.IgnoreCase);

        foreach (var definition in catalog.All.Where(definition => wildcard.IsMatch(definition.Name))
                   .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)) {
          WriteObject(definition);
        }
      }
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }
}
