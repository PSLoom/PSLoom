#Requires -Version 7.6
<#
.SYNOPSIS
  Validates release packages and creates a versioned module ZIP and package assets.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$Version,
  [string]$PackageDirectory = 'artifacts/packages',
  [string]$OutputDirectory = 'artifacts/release'
)

$ErrorActionPreference = 'Stop'
if ($Version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?$') {
  throw 'Expected a semantic version without build metadata.'
}
$core = ($Version -split '-')[0]
$ids = @('PSLoom.Warp', 'PSLoom.Build', 'PSLoom.TestKit', 'PSLoom')
$packages = foreach ($id in $ids) {
  $path = Join-Path $PackageDirectory "$id.$Version.nupkg"
  $zip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($path))
  try {
    $entry = $zip.GetEntry("$id.nuspec")
    if (-not $entry) { throw "$id has no nuspec." }
    $reader = [IO.StreamReader]::new($entry.Open())
    [xml]$nuspec = try { $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($nuspec.package.metadata.id -cne $id -or $nuspec.package.metadata.version -cne $Version) {
      throw "$id package identity/version does not match $Version."
    }
    if ($id -eq 'PSLoom') {
      foreach ($required in @('module/PSLoom.psd1', 'module/PSLoom.dll', 'module/Warp.dll')) {
        if (-not $zip.GetEntry($required)) { throw "Missing $required in $id." }
      }
    }
  }
  finally { $zip.Dispose() }
  Get-Item -LiteralPath $path
}

# Refuse stale assets instead of attaching packages from another version.
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $output) -and (Get-ChildItem -LiteralPath $output -Force)) {
  throw "Release output must be empty: $output"
}
$null = New-Item -ItemType Directory -Path $output -Force
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('psloom-release-' + [guid]::NewGuid().ToString('N'))
try {
  $unpacked = Join-Path $temporary 'unpacked'
  $moduleRoot = Join-Path $temporary 'archive/PSLoom'
  $destination = Join-Path $moduleRoot $core
  $package = $packages | Where-Object Name -CEQ ("PSLoom.$Version.nupkg")
  [IO.Compression.ZipFile]::ExtractToDirectory($package.FullName, $unpacked)
  $manifest = Import-PowerShellDataFile (Join-Path $unpacked 'module/PSLoom.psd1')
  if ($manifest.ModuleVersion -ne $core) { throw 'Module manifest version does not match release.' }
  $null = New-Item -ItemType Directory -Path $moduleRoot -Force
  Copy-Item -LiteralPath (Join-Path $unpacked 'module') -Destination $destination -Recurse
  [IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $temporary 'archive'), (Join-Path $output "PSLoom.$Version.zip"))
  foreach ($package in $packages) { Copy-Item -LiteralPath $package.FullName -Destination $output }
}
finally {
  $resolved = [IO.Path]::GetFullPath($temporary)
  if ([IO.Path]::GetDirectoryName($resolved) -ne [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath([IO.Path]::GetTempPath())) -or
      [IO.Path]::GetFileName($resolved) -notmatch '^psloom-release-[0-9a-f]{32}$') {
    throw "Unsafe temporary cleanup path: $resolved"
  }
  if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
