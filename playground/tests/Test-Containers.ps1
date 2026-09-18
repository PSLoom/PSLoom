#Requires -Version 7.6
param([switch]$Live)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
$manifest = Get-Content "$PSScriptRoot/../environments.json" -Raw | ConvertFrom-Json
foreach ($distro in 'ubuntu','arch') {
  if ($manifest.bases.$distro -notmatch '@sha256:[0-9a-f]{64}$') { throw 'Unpinned base' }
}
foreach ($version in '7.6.4','7.6.6') {
  if ($manifest.powershell.$version.sha256 -notmatch '^[0-9a-f]{64}$') { throw 'Invalid PowerShell checksum' }
}
if ($manifest.sdk.sha512 -notmatch '^[0-9a-f]{128}$') { throw 'Invalid SDK checksum' }
if ($manifest.sdk.version -ne (Get-Content "$root/global.json" -Raw | ConvertFrom-Json).sdk.version) { throw 'SDK mismatch' }
$script = Get-Content "$PSScriptRoot/../Invoke-Playground.ps1" -Raw
if ($script -notmatch "'--network','none'" -or $script -match "'--privileged'|docker.sock") { throw 'Unsafe container arguments' }
if ($Live) {
  foreach ($distro in 'ubuntu','arch') {
    & "$PSScriptRoot/../Invoke-Playground.ps1" -Mode Measure -Distribution $distro -Iterations 1
    if ($LASTEXITCODE) { throw "Container smoke failed: $distro" }
  }
}
Write-Host 'Container manifest and isolation checks passed.'
