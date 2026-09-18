#Requires -Version 7.6
param([Parameter(Mandatory)][string]$Version, [Parameter(Mandatory)][string]$Sha,
  [Parameter(Mandatory)][string]$Repository, [string]$Directory = 'artifacts/release')
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/Release.psm1" -Force
$config = Get-Content "$PSScriptRoot/../release.json" -Raw | ConvertFrom-Json
$dependency = ''
if ($config.prefix -eq 'reed') {
  [xml]$props = Get-Content Directory.Packages.props -Raw
  $dependency = [string]$props.Project.PropertyGroup.PSLoomVersion
}
$manifest = [ordered]@{
  schema = 1; repository = $Repository; sha = $Sha; version = $Version
  tag = "$($config.prefix)/v$Version"; channel = $(if ($Version.Contains('-')) { 'develop' } else { 'main' })
  sdkVersion = $dependency; packages = $config.packages
  assets = @(Get-ChildItem $Directory -File | ForEach-Object {
    @{ name = $_.Name; sha256 = (Get-FileHash $_.FullName).Hash }
  })
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $Directory 'release-manifest.json') -Encoding utf8
Assert-ReleaseAssets $Directory ([pscustomobject]$manifest)
