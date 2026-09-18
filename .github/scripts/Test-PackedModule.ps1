#Requires -Version 7.6
param([string]$Directory = 'artifacts/release')
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/Release.psm1" -Force
$manifest = Get-Content (Join-Path $Directory 'release-manifest.json') -Raw | ConvertFrom-Json
Assert-ReleaseAssets $Directory $manifest
$temp = Join-Path ([IO.Path]::GetTempPath()) ('packed-test-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory $temp
try {
  $moduleZip = @(Get-ChildItem $Directory -Filter '*.zip')
  if ($moduleZip.Count -ne 1) { throw 'Expected exactly one module archive.' }
  Expand-Archive -LiteralPath $moduleZip[0].FullName -DestinationPath $temp
  $env:PSModulePath = $temp + [IO.Path]::PathSeparator + $env:PSModulePath
  if ($manifest.repository -eq 'PSLoom/Reed') {
    # The build already restored the pinned kernel; never include its DLLs in Reed.
    $env:PSModulePath = (Join-Path $PWD 'artifacts/modules') + [IO.Path]::PathSeparator + $env:PSModulePath
    Import-Module PSLoom -ErrorAction Stop
    $reedManifest = Get-ChildItem $temp -Filter PSLoom.Reed.psd1 -Recurse | Select-Object -First 1
    Import-Module $reedManifest.FullName -ErrorAction Stop
    if (-not (Get-Module PSLoom.Reed)) { throw 'Reed import failed.' }
  } else {
    Import-Module PSLoom -ErrorAction Stop
    if (-not (Get-Command Invoke-Loom -Module PSLoom)) { throw 'Kernel command missing.' }
  }
}
finally {
  Remove-Module PSLoom.Reed,PSLoom -ErrorAction SilentlyContinue
  # Assemblies may stay locked on Windows; the operating system owns this temporary directory.
}
