@{
  RootModule = 'PSLoom.dll'
  ModuleVersion = '$version$'
  CompatiblePSEditions = @('Core')
  GUID = 'd08ff976-4d13-40d2-ba82-ce2988834fd2'

  Author = 'Bruno Sales'
  Copyright = '(c) 2026 Bruno Sales <me@baliestri.dev>. All rights reserved.'
  Description = 'The shell-ergonomics layer PowerShell never shipped with'

  PowerShellVersion = '7.6'

  FunctionsToExport = @()
  VariablesToExport = @()
  AliasesToExport = @()

  CmdletsToExport = @(
    'Invoke-Loom', 'Measure-Loom', 'Update-Harness',
    'Register-Hook', 'Unregister-Hook', 'Get-Hook', 'Trace-Hook',
    'Set-Style', 'Get-Style', 'Test-Style', 'Remove-Style', 'Get-StyleDefinition',
    'Register-StyleWatcher', 'Unregister-StyleWatcher', 'Trace-Style'
  )

  PrivateData = @{
    PSData = @{
      Tags = @('powershell', 'pwsh', 'psloom', 'loom', 'shell', 'zsh', 'fish')
      LicenseUri = 'https://github.com/baliestri/PSLoom/blob/main/LICENSE.md'
      ProjectUri = 'https://github.com/baliestri/PSLoom'
    }
  }
}
