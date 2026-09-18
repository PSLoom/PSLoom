#Requires -Version 7.6
<#
.SYNOPSIS
  Builds isolated consumers of the packed SDK and verifies versioning and concurrent module restoration.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$Directory,
  [Parameter(Mandatory)][string]$Version
)

$ErrorActionPreference = 'Stop'
$Directory = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Directory))
$packageFileVersion = ($Version -split '\+')[0]
$work = Join-Path $Directory ('.sdk-check-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $work

function Invoke-DotNet {
  param([string[]]$Arguments)
  & dotnet @Arguments
  if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $Arguments" }
}

function Assert-Assembly {
  param([string]$Path, [string]$AssemblyVersion, [string]$ProductVersion)
  $actual = [Reflection.AssemblyName]::GetAssemblyName($Path).Version.ToString()
  if ($actual -ne $AssemblyVersion) { throw "$Path assembly version is $actual, expected $AssemblyVersion" }
  $product = [Diagnostics.FileVersionInfo]::GetVersionInfo($Path).ProductVersion
  if ($product -ne $ProductVersion) { throw "$Path informational version is $product, expected $ProductVersion" }
}

try {
  & git -C $work init --quiet
  if ($LASTEXITCODE -ne 0) { throw 'Could not isolate the SDK verification Git context.' }
  foreach ($id in @('PSLoom.Build', 'PSLoom')) {
    [IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $Directory "$id.$packageFileVersion.nupkg"), (Join-Path $work $id))
  }
  # Isolate these consumers from the kernel repository's build customizations and package feeds.
  Set-Content (Join-Path $work 'Directory.Build.props') '<Project />'
  Set-Content (Join-Path $work 'Directory.Build.targets') '<Project />'
  Set-Content (Join-Path $work 'Directory.Packages.props') '<Project />'
  Set-Content (Join-Path $work 'nuget.config') '<configuration><packageSources><clear /></packageSources></configuration>'
  $escapedWork = [Security.SecurityElement]::Escape($work)
  foreach ($name in @('Consumer', 'Contract')) {
    $projectDirectory = Join-Path $work $name
    $null = New-Item -ItemType Directory -Path $projectDirectory
    $contract = if ($name -eq 'Contract') { '<ContractAssemblyVersion>1.0.0.0</ContractAssemblyVersion>' } else { '' }
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RepositoryRoot>$escapedWork/</RepositoryRoot>
    <VersionTagPrefix>psloom</VersionTagPrefix>
    <RequiresKernelModule>true</RequiresKernelModule>
    <PSLoomVersion>$Version</PSLoomVersion>
    <PkgPSLoom>$escapedWork/PSLoom</PkgPSLoom>
    $contract
  </PropertyGroup>
  <Import Project="../PSLoom.Build/buildTransitive/PSLoom.Build.props" />
  <Import Project="../PSLoom.Build/buildTransitive/PSLoom.Build.targets" />
</Project>
"@ | Set-Content (Join-Path $projectDirectory "$name.csproj")
    Set-Content (Join-Path $projectDirectory 'Example.cs') 'public sealed class Example { }'
    Invoke-DotNet @('restore', (Join-Path $projectDirectory "$name.csproj"), '--configfile', (Join-Path $work 'nuget.config'))
  }

  # Separate processes reproduce the integration-test/benchmark race on the shared kernel directory.
  $processes = @()
  try {
    foreach ($name in @('Consumer', 'Contract')) {
      $info = [Diagnostics.ProcessStartInfo]::new('dotnet')
      $info.UseShellExecute = $false
      $info.CreateNoWindow = $true
      $info.RedirectStandardOutput = $true
      $info.RedirectStandardError = $true
      foreach ($argument in @('build', (Join-Path $work "$name/$name.csproj"), '--no-restore', '-c', 'Release', '-p:VersionOverride=2.3.4-preview-one.2+build.7')) {
        $info.ArgumentList.Add($argument)
      }
      $running = [Diagnostics.Process]::Start($info)
      $processes += [pscustomobject]@{ Process = $running; Output = $running.StandardOutput.ReadToEndAsync(); Error = $running.StandardError.ReadToEndAsync() }
    }
    foreach ($entry in $processes) {
      $process = $entry.Process
      $process.WaitForExit()
      Write-Host $entry.Output.GetAwaiter().GetResult()
      Write-Host $entry.Error.GetAwaiter().GetResult()
      if ($process.ExitCode -ne 0) { throw 'Concurrent SDK consumer build failed.' }
    }
  }
  finally {
    foreach ($entry in $processes) {
      $process = $entry.Process
      if (-not $process.HasExited) { $process.WaitForExit() }
      $process.Dispose()
    }
  }
  Assert-Assembly (Join-Path $work 'Consumer/bin/Release/net10.0/Consumer.dll') '2.3.4.0' '2.3.4-preview-one.2+build.7'
  Assert-Assembly (Join-Path $work 'Contract/bin/Release/net10.0/Contract.dll') '1.0.0.0' '2.3.4-preview-one.2+build.7'
  $core = ($Version -split '[-+]')[0]
  $module = Join-Path $work "artifacts/modules/PSLoom/$core"
  foreach ($file in Get-ChildItem (Join-Path $work 'PSLoom/module') -File -Recurse) {
    $relative = [IO.Path]::GetRelativePath((Join-Path $work 'PSLoom/module'), $file.FullName)
    if ((Get-FileHash $file.FullName).Hash -ne (Get-FileHash (Join-Path $module $relative)).Hash) { throw "Restore content differs: $relative" }
  }
  # Repeating restore must not rewrite files that a running pwsh process may have loaded.
  $before = (Get-Item (Join-Path $module 'Warp.dll')).LastWriteTimeUtc
  Invoke-DotNet @('build', (Join-Path $work 'Consumer/Consumer.csproj'), '--no-restore', '-c', 'Release')
  if ((Get-Item (Join-Path $module 'Warp.dll')).LastWriteTimeUtc -ne $before) { throw 'An identical restore rewrote Warp.dll.' }
  Assert-Assembly (Join-Path $work 'Consumer/bin/Release/net10.0/Consumer.dll') '0.0.0.0' '0.0.0'

  # A new prerelease at the same numeric version must replace content and remove stale files.
  $upgrade = Join-Path $work 'upgraded-package'
  $null = New-Item -ItemType Directory -Path $upgrade
  Copy-Item -LiteralPath (Join-Path $work 'PSLoom/module') -Destination $upgrade -Recurse
  Set-Content (Join-Path $upgrade 'module/upgrade.txt') 'new package content'
  Set-Content (Join-Path $module 'obsolete.txt') 'stale package content'
  $upgradedVersion = "$core-sdk-upgrade.1"
  Invoke-DotNet @('build', (Join-Path $work 'Consumer/Consumer.csproj'), '--no-restore', '-c', 'Release',
    "-p:PSLoomVersion=$upgradedVersion", "-p:PkgPSLoom=$upgrade")
  if (-not (Test-Path (Join-Path $module 'upgrade.txt')) -or (Test-Path (Join-Path $module 'obsolete.txt'))) {
    throw 'Same-core package upgrade did not replace the module content.'
  }
  # Also exercise changing the numeric version directory.
  $nextCore = '9.8.7'
  $manifestPath = Join-Path $upgrade 'module/PSLoom.psd1'
  (Get-Content $manifestPath -Raw).Replace("ModuleVersion = '$core'", "ModuleVersion = '$nextCore'") | Set-Content $manifestPath
  Invoke-DotNet @('build', (Join-Path $work 'Consumer/Consumer.csproj'), '--no-restore', '-c', 'Release',
    "-p:PSLoomVersion=$nextCore-sdk-upgrade.2", "-p:PkgPSLoom=$upgrade")
  $versions = @(Get-ChildItem (Join-Path $work 'artifacts/modules/PSLoom') -Directory)
  if ($versions.Count -ne 1 -or $versions[0].Name -ne $nextCore) { throw 'Numeric version upgrade left an old module directory.' }
  # A failed restore must fail the build rather than silently retaining an old module.
  & dotnet build (Join-Path $work 'Consumer/Consumer.csproj') --no-restore -c Release "-p:PkgPSLoom=$work/missing" *> (Join-Path $work 'missing-module.log')
  if ($LASTEXITCODE -eq 0 -or (Get-Content (Join-Path $work 'missing-module.log') -Raw) -notmatch 'no module/PSLoom.psd1') {
    throw 'Missing kernel module did not produce the expected failure.'
  }

  & dotnet build (Join-Path $work 'Consumer/Consumer.csproj') --no-restore -c Release '-p:VersionOverride=1.2-bad' *> (Join-Path $work 'invalid-version.log')
  if ($LASTEXITCODE -eq 0) { throw 'Invalid VersionOverride was accepted.' }
  if ((Get-Content (Join-Path $work 'invalid-version.log') -Raw) -notmatch 'Invalid semantic version') { throw 'Invalid-version build failed for an unrelated reason.' }
  Write-Host 'Packed SDK checks passed: assembly versions, override/fallback, concurrent/repeat restore, upgrades, and missing-module failure.'
}
finally {
  $resolved = [IO.Path]::GetFullPath($work)
  if ([IO.Path]::GetDirectoryName($resolved) -ne $Directory) { throw "Unsafe SDK verification cleanup: $resolved" }
  Remove-Item -LiteralPath $resolved -Recurse -Force
}

# Expected negative dotnet checks above leave LASTEXITCODE nonzero; signal the successful verification explicitly.
exit 0
