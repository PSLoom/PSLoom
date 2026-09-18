#Requires -Version 7.6
param(
  [ValidateSet('alias-auto','alias-explicit','typical','eager')][string]$Scenario,
  [string]$RepositoryRoot = [IO.Path]::GetFullPath("$PSScriptRoot/.."),
  [switch]$PrepareCache
)
$ErrorActionPreference = 'Stop'
# Core cmdlets only before measurement: Get-Module does not autoload Utility.
$loadedBefore = @(foreach ($module in Get-Module) { $module.Name })
if ($loadedBefore -contains 'Microsoft.PowerShell.Utility') { throw 'Utility was loaded before the probe.' }
$metrics = [ordered]@{}
$timings = @()
$watch = [Diagnostics.Stopwatch]::new()
switch ($Scenario) {
  'alias-auto' {
    $watch.Start()
    Set-Alias -Name k -Value kubectl -Scope Global
    $watch.Stop()
    $metrics.firstMs = $watch.Elapsed.TotalMilliseconds
    $watch.Restart()
    Set-Alias -Name k -Value kubectl -Scope Global
    $watch.Stop()
    $metrics.secondMs = $watch.Elapsed.TotalMilliseconds
    $metrics.totalMs = $metrics.firstMs + $metrics.secondMs
  }
  'alias-explicit' {
    $watch.Start()
    Import-Module Microsoft.PowerShell.Utility
    $watch.Stop()
    $metrics.importMs = $watch.Elapsed.TotalMilliseconds
    $watch.Restart()
    Set-Alias -Name k -Value kubectl -Scope Global
    $watch.Stop()
    $metrics.aliasMs = $watch.Elapsed.TotalMilliseconds
    $metrics.totalMs = $metrics.importMs + $metrics.aliasMs
  }
  default {
    $env:LOOM_INTERACTIVE = '1'
    $env:PSModulePath = [IO.Path]::Combine($RepositoryRoot, 'artifacts', 'modules') +
      [IO.Path]::PathSeparator + $env:PSModulePath
    $watch.Start()
    Import-Module PSLoom
    $watch.Stop()
    $metrics.importMs = $watch.Elapsed.TotalMilliseconds
    $type = (Get-Module PSLoom).ImplementingAssembly.GetType('PSLoom.Runtime.Loom.LoomSession', $true)
    $sessions = $type.GetProperty('PerRunspace').GetValue($null)
    $session = $sessions.GetType().GetMethod('ForCurrent').Invoke($sessions, @())
    $null = $type.GetProperty('FirstParty').GetValue($session).Add('Fixture')
    $draft = [IO.Path]::Combine($RepositoryRoot, 'benchmarks', 'drafts', "$Scenario.ps1")
    $watch.Restart()
    $null = . $draft
    $watch.Stop()
    $metrics.draftMs = $watch.Elapsed.TotalMilliseconds
    $metrics.totalMs = $metrics.importMs + $metrics.draftMs
    $timings = @(foreach ($row in Measure-Loom) {
      @{ phase=$row.Phase.ToString(); name=$row.Name; line=$row.Line; depth=$row.Depth;
        inclusiveMs=$row.Inclusive.TotalMilliseconds; exclusiveMs=$row.Exclusive.TotalMilliseconds }
    })
  }
}
$loadedAfter = @(foreach ($module in Get-Module) { $module.Name })
$moduleDetails = @(foreach ($module in Get-Module) { @{name=$module.Name; version=$module.Version.ToString(); path=$module.Path} })
if ($PrepareCache) {
  # Force discovery only in the unmeasured preparation process. PowerShell queues
  # its cache write asynchronously; wait for a real readable cache, not a delay.
  $null = Get-Command Set-Alias, ConvertTo-Json
  $deadline = [DateTime]::UtcNow.AddSeconds(45)
  $ready = $false
  while ([DateTime]::UtcNow -lt $deadline) {
    try {
      if ([IO.File]::Exists($env:PSModuleAnalysisCachePath)) {
        $cacheBytes = [IO.File]::ReadAllBytes($env:PSModuleAnalysisCachePath)
        if ($cacheBytes.Length -gt 16 -and [Text.Encoding]::UTF8.GetString($cacheBytes).Contains('Set-Alias')) {
          $ready = $true
          break
        }
      }
    } catch [IO.IOException] { }
    [Threading.Thread]::Sleep(100)
  }
  if (-not $ready) { throw 'Preparation did not persist a module analysis cache containing Set-Alias.' }
}
$result = [ordered]@{
  schemaVersion=1; scenario=$Scenario; pid=$PID; metrics=$metrics; loomTimings=$timings
  environment=@{
    powerShell=$PSVersionTable.PSVersion.ToString(); runtime=[Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
    os=[Runtime.InteropServices.RuntimeInformation]::OSDescription
    architecture=[Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    psHome=$PSHOME; processPath=[Environment]::ProcessPath; modulePaths=$env:PSModulePath -split [IO.Path]::PathSeparator
    analysisCache=$env:PSModuleAnalysisCachePath; loadedBefore=$loadedBefore; loadedAfter=$loadedAfter; modules=$moduleDetails
  }
}
# Serialization may autoload Utility; it is deliberately after every stopwatch.
$result | ConvertTo-Json -Depth 12 -Compress
