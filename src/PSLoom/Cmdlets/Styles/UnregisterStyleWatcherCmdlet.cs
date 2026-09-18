// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Styles;
using PSLoom.Warp;

namespace PSLoom.Cmdlets.Styles;

/// <summary>
///   Removes a style watcher.
/// </summary>
[Cmdlet(VerbsLifecycle.Unregister, "StyleWatcher")]
public sealed class UnregisterStyleWatcherCmdlet : PSCmdlet {
  /// <summary>
  ///   Gets or sets the watcher registration identifier.
  /// </summary>
  [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
  public Guid Id { get; set; }

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      if (!StyleStore.PerRunspace.ForCurrent().RemoveWatcher(Id)) {
        WriteWarning($"No style watcher with Id '{Id}' was found.");
      }
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }
}
