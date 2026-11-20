#requires -Version 7.4
<#
.SYNOPSIS
  Opens a pwsh session without a profile in which the modules built in this workspace resolve by name.
.DESCRIPTION
  Puts artifacts/modules of the given repository and of the neighbor repositories pinned by its .ref files at
  the front of PSModulePath, so that Import-Module and RequiredModules resolve the same way they do for
  installed modules.
.PARAMETER Path
  The root of the repository being worked on; the current directory when omitted.
.PARAMETER Command
  Runs this text and exits, instead of opening an interactive session.
#>
[CmdletBinding()]
param(
  [string] $Path = (Get-Location).Path,
  [string] $Command
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$moduleDirectories = @(& (Join-Path $PSScriptRoot 'Get-ModulePath.ps1') -Path $Path)

if ($moduleDirectories.Count -eq 0) {
  throw 'No built modules were found. Run dotnet build first.'
}

# PSModulePath is set inside the session: pwsh reorders an inherited value at startup.
$quoted = ($moduleDirectories | ForEach-Object { "'$($_.Replace("'", "''"))'" }) -join ', '
$prelude = "`$env:PSModulePath = (@($quoted) + `$env:PSModulePath) -join [System.IO.Path]::PathSeparator"

if ($Command) {
  & pwsh -NoLogo -NoProfile -NonInteractive -Command "$prelude`n$Command"
}
else {
  & pwsh -NoLogo -NoProfile -NoExit -Command $prelude
}

exit $LASTEXITCODE
