#Requires -Version 7.6
param(
  [string]$PowerShellPath,
  [ValidateSet('alias-auto','alias-explicit','typical','eager')][string[]]$Scenario = @('alias-auto','alias-explicit','typical','eager'),
  [ValidateSet('fresh','reused')][string[]]$CacheMode = @('fresh','reused'),
  [ValidateRange(1,1000)][int]$Iterations = 10,
  [ValidateRange(1,600)][int]$TimeoutSeconds = 60,
  [string]$OutputRoot = "$PSScriptRoot/../artifacts/playground"
)
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/Diagnostics.psm1" -Force
$root = [IO.Path]::GetFullPath("$PSScriptRoot/..")
$prefix = @()
if (-not $PowerShellPath) {
  $PowerShellPath = [Environment]::ProcessPath
  if ([IO.Path]::GetFileNameWithoutExtension($PowerShellPath) -eq 'dotnet') { $prefix = @(Join-Path $PSHOME 'pwsh.dll') }
}
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + [guid]::NewGuid().ToString('N')
$output = [IO.Path]::GetFullPath((Join-Path $OutputRoot $runId))
$null = [IO.Directory]::CreateDirectory($output)
$reportPath = Join-Path $output 'report.json'
$report = [ordered]@{schemaVersion=1; status='failed'; runId=$runId; source=$null; environment=$null;
  preparations=@(); samples=@(); summaries=@(); errors=@()}
function Save-Report { $report | ConvertTo-Json -Depth 24 | Set-Content -LiteralPath $reportPath -Encoding utf8 }
function Invoke-Sample([string]$Name, [string]$Mode, [int]$Index, [string]$CacheRoot, [bool]$Prepare) {
  $null = [IO.Directory]::CreateDirectory($CacheRoot)
  $cacheFile = Join-Path $CacheRoot 'ModuleAnalysisCache'
  $environment = @{
    PSModuleAnalysisCachePath=$cacheFile; PSDisableModuleAnalysisCacheCleanup='1'
    HOME=(Join-Path $CacheRoot 'home'); USERPROFILE=(Join-Path $CacheRoot 'home')
    XDG_CACHE_HOME=(Join-Path $CacheRoot 'cache'); XDG_CONFIG_HOME=(Join-Path $CacheRoot 'config')
    XDG_DATA_HOME=(Join-Path $CacheRoot 'data'); LOCALAPPDATA=(Join-Path $CacheRoot 'local')
    APPDATA=(Join-Path $CacheRoot 'roaming'); PSModulePath=''
  }
  foreach ($key in @('HOME','XDG_CACHE_HOME','XDG_CONFIG_HOME','XDG_DATA_HOME','LOCALAPPDATA','APPDATA')) {
    $null = [IO.Directory]::CreateDirectory($environment[$key])
  }
  $before = Get-PlaygroundCacheState $cacheFile
  if ($Mode -eq 'fresh' -and $before.exists) { throw 'Fresh sample cache already exists.' }
  if ($Mode -eq 'reused' -and -not $Prepare -and -not $before.exists) { throw 'Prepared cache missing.' }
  $arguments = $prefix + @('-NoProfile','-NonInteractive','-File',"$PSScriptRoot/Probe.ps1",'-Scenario',$Name,'-RepositoryRoot',$root)
  if ($Prepare) { $arguments += '-PrepareCache' }
  $result = Invoke-PlaygroundProcess $PowerShellPath $arguments $environment $TimeoutSeconds $root
  $label = "$Name-$Mode-$Index"
  $result.stdout | Set-Content -LiteralPath (Join-Path $output "$label.stdout.log") -Encoding utf8
  $result.stderr | Set-Content -LiteralPath (Join-Path $output "$label.stderr.log") -Encoding utf8
  if ($result.timedOut) { throw "Timed out: $label" }
  if ($result.exitCode -ne 0) { throw "Probe failed ($($result.exitCode)): $label. See stderr log." }
  $sample = $result.stdout | ConvertFrom-Json
  Assert-PlaygroundSample $sample
  if ($sample.scenario -cne $Name) { throw 'Unexpected scenario in probe response.' }
  $after = Get-PlaygroundCacheState $cacheFile
  if ($Prepare -and (-not $after.exists -or $after.bytes -le 16)) { throw 'Cache preparation not persisted.' }
  return [pscustomobject]@{scenario=$Name; cacheMode=$Mode; index=$Index; cacheBefore=$before; cacheAfter=$after; result=$sample}
}
try {
  if (Test-Path -LiteralPath "$root/source-manifest.json") {
    $report.source = Get-Content "$root/source-manifest.json" -Raw | ConvertFrom-Json
  } else {
    Import-Module "$PSScriptRoot/SourceContext.psm1" -Force
    $report.source = Get-PlaygroundSource $root
  }
  $report.environment = @{ runnerPowerShell=$PSVersionTable.PSVersion.ToString();
    targetExecutable=$PowerShellPath; cachePolicy='Isolated module analysis cache; OS page cache is not reset';
    kernel=[Environment]::OSVersion.VersionString;
    osDescription=[Runtime.InteropServices.RuntimeInformation]::OSDescription }
  if (Test-Path "$root/playground-environment.json") {
    $report.environment.container = Get-Content "$root/playground-environment.json" -Raw | ConvertFrom-Json
  }
  foreach ($name in $Scenario) {
    foreach ($mode in $CacheMode) {
      $shared = Join-Path $output "cache/$name-$mode"
      if ($mode -eq 'reused') {
        Write-Host "Preparing persisted cache: $name"
        $report.preparations += Invoke-Sample $name $mode 0 $shared $true
        Save-Report
      }
      for ($index=1; $index -le $Iterations; $index++) {
        $cacheRoot = if ($mode -eq 'fresh') { "$shared/$index" } else { $shared }
        $report.samples += Invoke-Sample $name $mode $index $cacheRoot $false
        Save-Report
      }
    }
  }
  foreach ($group in ($report.samples | Group-Object scenario,cacheMode)) {
    foreach ($metric in $group.Group[0].result.metrics.PSObject.Properties.Name) {
      $values = @($group.Group | ForEach-Object { $_.result.metrics.$metric })
      $report.summaries += [pscustomobject]@{scenario=$group.Group[0].scenario; cacheMode=$group.Group[0].cacheMode;
        metric=$metric; count=$values.Count; medianMs=(Get-PlaygroundMedian $values)}
    }
  }
  $report.status = 'complete'
} catch {
  $report.errors += $_.Exception.Message
  $report.summaries = @()
} finally { Save-Report }
$summary = $report.summaries | Format-Table scenario,cacheMode,metric,count,medianMs -AutoSize | Out-String
$summary | Set-Content -LiteralPath (Join-Path $output 'summary.txt') -Encoding utf8
Write-Host $summary
Write-Host "Report: $reportPath"
if ($report.status -ne 'complete') { throw ($report.errors -join '; ') }
