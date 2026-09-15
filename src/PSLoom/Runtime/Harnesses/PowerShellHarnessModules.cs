// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PSLoom.Runtime.Harnesses;

/// <summary>
///   Module operations through the running cmdlet: <c>Import-Module</c> and PSResourceGet's <c>Install-PSResource</c>.
/// </summary>
internal sealed class PowerShellHarnessModules(PSCmdlet cmdlet) : IHarnessModules {
  internal const string HOST_TAG = "PSHOST";

  private const string IMPORT_SCRIPT =
    """
    param($Name, $Version)
    if ($Version) { Import-Module -Name $Name -RequiredVersion $Version -Global -ErrorAction Stop }
    else { Import-Module -Name $Name -Global -ErrorAction Stop }
    """;

  // -TrustRepository: PSGallery is untrusted by default and would prompt, which hangs a profile.
  private const string INSTALL_SCRIPT =
    """
    param($Name, $Version)
    if (-not (Get-Command Install-PSResource -ErrorAction Ignore)) {
      throw 'PSResourceGet is not available: Install-PSResource was not found.'
    }
    $parameters = @{ Name = $Name; Repository = 'PSGallery'; Scope = 'CurrentUser'; TrustRepository = $true; ErrorAction = 'Stop' }
    if ($Version) { $parameters.Version = $Version }
    Install-PSResource @parameters
    """;

  /// <inheritdoc />
  public ModuleImportResult Import(string moduleName, Version? version) {
    try {
      cmdlet.InvokeCommand.InvokeScript(IMPORT_SCRIPT, moduleName, version?.ToString());
      return new ModuleImportResult(ModuleImportStatus.Imported);
    }
    catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException)) {
      return new ModuleImportResult(IsModuleNotFound(exception) ? ModuleImportStatus.NotFound : ModuleImportStatus.Failed, exception);
    }
  }

  /// <inheritdoc />
  public void Install(string moduleName, Version? version)
    => cmdlet.InvokeCommand.InvokeScript(INSTALL_SCRIPT, moduleName, version?.ToString());

  /// <inheritdoc />
  public void WriteProgress(string message)
    => cmdlet.WriteInformation(new HostInformationMessage { Message = message }, [HOST_TAG]);

  /// <summary>
  ///   Recognizes <c>Modules_ModuleNotFound</c> and <c>Modules_ModuleWithVersionNotFound</c> anywhere in the exception chain.
  /// </summary>
  internal static bool IsModuleNotFound(Exception exception) {
    for (var current = exception; current is not null; current = current.InnerException) {
      if (current is IContainsErrorRecord { ErrorRecord.FullyQualifiedErrorId: { } errorId } &&
          (errorId.StartsWith("Modules_ModuleNotFound", StringComparison.Ordinal) ||
           errorId.StartsWith("Modules_ModuleWithVersionNotFound", StringComparison.Ordinal))) {
        return true;
      }
    }

    return false;
  }
}
