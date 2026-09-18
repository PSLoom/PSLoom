#Requires -Version 7.6
<#
.SYNOPSIS
  Packs the four kernel packages for local harness development and checks their content.

.DESCRIPTION
  Builds Release and packs PSLoom.Warp, PSLoom.Build, PSLoom.TestKit and PSLoom with one version into a local folder. A harness
  then builds against them with: dotnet build -p:PSLoomVersion=<printed version>

.PARAMETER Output
  Destination folder. Default: ~/.psloom/packages.

.PARAMETER Version
  Package version. Default: 0.0.0-local.<yyyyMMddHHmmss>, unique per run so NuGet's global cache never serves a stale package.
#>
[CmdletBinding()]
param(
  [string]$Output = (Join-Path $HOME '.psloom' 'packages'),
  [string]$Version = ('0.0.0-local.' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$Output = [IO.Path]::GetFullPath($Output)
$packageFileVersion = ($Version -split '\+')[0]
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
  throw "Invalid package version '$Version'."
}
$null = New-Item -ItemType Directory -Force -Path $Output
foreach ($id in @('PSLoom.Warp', 'PSLoom.Build', 'PSLoom.TestKit', 'PSLoom')) {
  if (Test-Path -LiteralPath (Join-Path $Output "$id.$packageFileVersion.nupkg")) {
    throw "Package $id.$Version already exists. Choose a new version to avoid stale NuGet caches."
  }
}

$projects = @(
  'src/Warp/Warp.csproj'
  'src/PSLoom.Build/PSLoom.Build.csproj'
  'tests/PSLoom.TestKit/PSLoom.TestKit.csproj'
  'src/PSLoom/PSLoom.csproj'
)

foreach ($project in $projects) {
  dotnet pack (Join-Path $root $project) --configuration Release --output $Output "-p:VersionOverride=$Version"
  if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed for $project." }
}

& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Test-Packages.ps1') -Directory $Output -Version $Version
if ($LASTEXITCODE -ne 0) { throw 'Package check failed.' }

$Version
