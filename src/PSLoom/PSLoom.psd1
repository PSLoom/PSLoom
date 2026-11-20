@{
  RootModule = 'PSLoom.dll'
  ModuleVersion = '$version$'
  GUID = '455dfb5a-04d8-4a9d-8529-5f5123124785'
  Author = 'Bruno Sales'
  CompanyName = 'baliestri.dev'
  Copyright = '(c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.'
  Description = 'The shell-ergonomics layer PowerShell never shipped with'
  PowerShellVersion = '7.6'
  CompatiblePSEditions = @('Core')
  CmdletsToExport = @($cmdlets$)
  FunctionsToExport = @()
  AliasesToExport = @()
  VariablesToExport = @()
  RequiredModules = @($requiredModules$)
  PrivateData = @{
    PSData = @{
      Tags = @('powershell', 'pwsh', 'shell', 'psloom')
      ProjectUri = 'https://github.com/PSLoom/PSLoom'
      LicenseUri = 'https://github.com/PSLoom/PSLoom/blob/main/LICENSE.md'
      $prerelease$
    }
  }
}
