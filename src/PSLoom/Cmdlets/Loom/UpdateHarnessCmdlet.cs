// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PSLoom.Runtime.Harnesses;
using PSLoom.Runtime.Loom;
using PSLoom.Warp;

namespace PSLoom.Cmdlets.Loom;

/// <summary>
///   Updates installed first-party harness modules from PSGallery. Drafts never update harnesses; this is the only way, and the
///   new version is used from the next session.
/// </summary>
[Cmdlet(VerbsData.Update, "Harness", SupportsShouldProcess = true)]
public sealed class UpdateHarnessCmdlet : PSCmdlet {
  private const string INSTALLED_SCRIPT =
    """
    param($Name)
    if (Get-Command Get-InstalledPSResource -ErrorAction Ignore) {
      Get-InstalledPSResource -Name $Name -Scope CurrentUser -ErrorAction Ignore
    }
    """;

  private const string UPDATE_SCRIPT =
    """
    param($Name)
    if (-not (Get-Command Update-PSResource -ErrorAction Ignore)) {
      throw 'PSResourceGet is not available: Update-PSResource was not found.'
    }
    Update-PSResource -Name $Name -Repository PSGallery -Scope CurrentUser -TrustRepository -PassThru -ErrorAction Stop
    """;

  /// <summary>
  ///   Gets or sets the harness names to update; every installed first-party harness when omitted.
  /// </summary>
  [Parameter(Position = 0)]
  public string[]? Name { get; set; }

  /// <inheritdoc />
  protected override void ProcessRecord() {
    try {
      var session = LoomSession.PerRunspace.ForCurrent();
      var explicitNames = Name is { Length: > 0 };
      var updated = false;

      foreach (var harness in explicitNames ? Name!.Distinct(StringComparer.OrdinalIgnoreCase) : session.FirstParty.Order(StringComparer.OrdinalIgnoreCase)) {
        if (!session.FirstParty.Contains(harness)) {
          WriteError(LoomException.HarnessNotFirstParty(harness, session.FirstParty).ToErrorRecord());
          continue;
        }

        var moduleName = FirstPartyHarnesses.ModuleName(harness);

        if (InvokeCommand.InvokeScript(INSTALLED_SCRIPT, moduleName).Count == 0) {
          if (explicitNames) {
            WriteWarning($"{moduleName} is not installed for the current user; nothing to update.");
          }

          continue;
        }

        if (!ShouldProcess(moduleName, "Update from PSGallery")) {
          continue;
        }

        updated |= Update(session, moduleName);
      }

      if (updated) {
        WriteWarning("Updated harnesses take effect in the next PowerShell session.");
      }
    }
    catch (PowerShellException exception) {
      WriteError(exception.ToErrorRecord());
    }
  }

  private bool Update(LoomSession session, string moduleName) {
    try {
      using (session.InstallLock.Acquire(WriteVerbose)) {
        WriteObject(InvokeCommand.InvokeScript(UPDATE_SCRIPT, moduleName), true);
        return true;
      }
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException or PipelineStoppedException)) {
      WriteError(LoomException.HarnessUpdateFailed(moduleName, exception).ToErrorRecord());
      return false;
    }
  }
}
