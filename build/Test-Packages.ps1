#Requires -Version 7.6
<#
.SYNOPSIS
  Asserts the content of the four kernel packages: PSLoom.Warp, PSLoom.Build, PSLoom.TestKit and PSLoom.

.PARAMETER Directory
  The folder holding the .nupkg files.

.PARAMETER Version
  The package version every package must carry.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$Directory,
  [Parameter(Mandatory)][string]$Version
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$core = ($Version -split '[-+]')[0]
$Directory = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Directory))
$packageFileVersion = ($Version -split '\+')[0]

$expectations = @(
  [pscustomobject]@{ Id = 'PSLoom.Warp'; Present = @('lib/net10.0/Warp.dll'); Absent = @(); Dependencies = @() }
  [pscustomobject]@{
    Id = 'PSLoom.Build'
    Present = @('buildTransitive/PSLoom.Build.props', 'buildTransitive/PSLoom.Build.targets', 'buildTransitive/RestoreModule.cs')
    Absent = @('lib/')
    Dependencies = @()
  }
  [pscustomobject]@{
    Id = 'PSLoom.TestKit'
    Present = @('lib/net10.0/PSLoom.TestKit.dll')
    Absent = @()
    Dependencies = @('Microsoft.PowerShell.SDK')
  }
  [pscustomobject]@{
    Id = 'PSLoom'
    Present = @('lib/net10.0/PSLoom.dll', 'module/PSLoom.psd1', 'module/PSLoom.dll', 'module/Warp.dll')
    Absent = @()
    Dependencies = @('PSLoom.Warp')
  }
)

$failures = [Collections.Generic.List[string]]::new()

foreach ($expected in $expectations) {
  $path = Join-Path $Directory "$($expected.Id).$packageFileVersion.nupkg"

  if (-not (Test-Path -LiteralPath $path)) {
    $failures.Add("$($expected.Id): missing $path")
    continue
  }

  $zip = [IO.Compression.ZipFile]::OpenRead($path)

  try {
    $entries = @($zip.Entries | ForEach-Object FullName)

    foreach ($entry in @($expected.Present) + @('README.md', 'LICENSE.md')) {
      if ($entries -notcontains $entry) { $failures.Add("$($expected.Id): no entry $entry") }
    }

    foreach ($prefix in $expected.Absent) {
      if ($entries | Where-Object { $_.StartsWith($prefix) }) { $failures.Add("$($expected.Id): unexpected entries under $prefix") }
    }

    $nuspecEntry = $zip.Entries | Where-Object { $_.FullName -eq "$($expected.Id).nuspec" }
    $reader = [IO.StreamReader]::new($nuspecEntry.Open())
    [xml]$nuspec = try { $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($nuspec.package.metadata.id -ne $expected.Id -or $nuspec.package.metadata.version -ne $Version) {
      $failures.Add("$($expected.Id): nuspec identity/version does not match $Version")
    }
    $dependencies = @($nuspec.SelectNodes("//*[local-name()='dependency']"))
    if ($expected.Id -in @('PSLoom.Warp', 'PSLoom.Build') -and $dependencies.Count -gt 0) {
      $failures.Add("$($expected.Id): unexpected dependencies")
    }

    foreach ($dependencyId in $expected.Dependencies) {
      $dependency = $dependencies | Where-Object id -EQ $dependencyId

      if (-not $dependency) {
        $failures.Add("$($expected.Id): no dependency on $dependencyId")
      }
      elseif ($dependencyId -like 'PSLoom*' -and $dependency.version -notin @($Version, "[$Version, )", $packageFileVersion, "[$packageFileVersion, )")) {
        $failures.Add("$($expected.Id): depends on $dependencyId $($dependency.version), expected $Version")
      }
    }

    if ($expected.Id -eq 'PSLoom') {
      $manifestEntry = $zip.Entries | Where-Object { $_.FullName -eq 'module/PSLoom.psd1' }

      if ($manifestEntry) {
        $manifestReader = [IO.StreamReader]::new($manifestEntry.Open())
        $manifestText = try { $manifestReader.ReadToEnd() } finally { $manifestReader.Dispose() }

        if ($manifestText -notmatch "ModuleVersion\s*=\s*'$([regex]::Escape($core))'") {
          $failures.Add("PSLoom: module/PSLoom.psd1 ModuleVersion is not '$core'")
        }
      }
    }
  }
  finally {
    $zip.Dispose()
  }
}

# Inspect the real managed PE metadata, then import the packed module in a fresh process.
$verificationRoot = Join-Path $Directory ('.verify-' + [Guid]::NewGuid().ToString('N'))
try {
  foreach ($id in @('PSLoom.Warp', 'PSLoom.TestKit', 'PSLoom')) {
    $package = Join-Path $Directory "$id.$packageFileVersion.nupkg"
    if (-not (Test-Path -LiteralPath $package)) { continue }
    $unpacked = Join-Path $verificationRoot $id
    [IO.Compression.ZipFile]::ExtractToDirectory($package, $unpacked)
    foreach ($dll in @(Get-ChildItem -LiteralPath $unpacked -Filter '*.dll' -Recurse)) {
      $assembly = [Reflection.AssemblyName]::GetAssemblyName($dll.FullName)
      $expectedVersion = if ($assembly.Name -eq 'Warp') { '1.0.0.0' } else { "$core.0" }
      if ($assembly.Version.ToString() -ne $expectedVersion) {
        $failures.Add("$id/$($dll.Name): assembly version $($assembly.Version), expected $expectedVersion")
      }
      $metadata = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll.FullName)
      if (($metadata.ProductVersion -split '\+')[0] -ne ($Version -split '\+')[0]) {
        $failures.Add("$id/$($dll.Name): informational version $($metadata.ProductVersion), expected $Version")
      }
      if ($metadata.FileVersion -notin @($core, "$core.0")) {
        $failures.Add("$id/$($dll.Name): file version $($metadata.FileVersion), expected $core.0")
      }
    }
  }
  $module = Join-Path $verificationRoot 'PSLoom/module/PSLoom.psd1'
  if (Test-Path -LiteralPath $module) {
    & pwsh -NoProfile -NonInteractive -CommandWithArgs '
      $ErrorActionPreference = "Stop"
      $manifest = $args[0]
      $expected = $args[1]
      $module = Import-Module -Name $manifest -PassThru
      if ($module.Version.ToString() -ne $expected) { throw "Imported module version mismatch" }
      if (-not (Get-Command Invoke-Loom -Module PSLoom)) { throw "Kernel command missing" }
      $warp = @([AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq "Warp" })
      if ($warp.Count -ne 1 -or $warp[0].GetName().Version.ToString() -ne "1.0.0.0") { throw "Warp contract mismatch" }
      if ([IO.Path]::GetDirectoryName($warp[0].Location) -ne [IO.Path]::GetDirectoryName($manifest)) { throw "Warp loaded outside the kernel module" }
    ' $module $core
    if ($LASTEXITCODE -ne 0) { $failures.Add('PSLoom: packed module import failed') }
  }
}
catch {
  $failures.Add("Package runtime verification failed: $_")
}
finally {
  if (Test-Path -LiteralPath $verificationRoot) {
    # The generated child is resolved under Directory before recursive removal.
    $resolved = [IO.Path]::GetFullPath($verificationRoot)
    if ([IO.Path]::GetDirectoryName($resolved) -ne $Directory) { throw "Unsafe verification cleanup: $resolved" }
    Remove-Item -LiteralPath $resolved -Recurse -Force
  }
}

if ($failures.Count -gt 0) {
  $failures | ForEach-Object { Write-Host "FAIL $_" }
  exit 1
}

Write-Host "All four packages at $Version look right."
exit 0
