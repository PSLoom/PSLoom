#requires -Version 7.4
<#
.SYNOPSIS
  Packs every module the build assembled into a .nupkg, the format the PowerShell Gallery receives.
.DESCRIPTION
  Reads artifacts/modules of the repository and writes one <Module>.<version>.nupkg per module. The same file
  is attached to the GitHub release and published to the Gallery, so what is published is what was released.
.PARAMETER Path
  The root of the repository; the current directory when omitted.
.PARAMETER Destination
  Where the packages are written; artifacts/packages of the repository when omitted.
#>
[CmdletBinding()]
param(
  [string] $Path = (Get-Location).Path,
  [string] $Destination
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not $Destination) {
  $Destination = Join-Path $Path 'artifacts/packages'
}

$modulesRoot = Join-Path $Path 'artifacts/modules'
$modules = @(if (Test-Path -LiteralPath $modulesRoot) { Get-ChildItem -LiteralPath $modulesRoot -Directory })

if ($modules.Count -eq 0) {
  throw 'No built modules were found. Run dotnet build first.'
}

$null = New-Item -ItemType Directory -Force -Path $Destination
$Destination = (Resolve-Path -LiteralPath $Destination).Path

foreach ($module in $modules) {
  Compress-PSResource -Path $module.FullName -DestinationPath $Destination
}

Get-ChildItem -LiteralPath $Destination -Filter *.nupkg | ForEach-Object { $_.FullName }
