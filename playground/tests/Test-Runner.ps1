#Requires -Version 7.6
param([switch]$Integration)
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/../Diagnostics.psm1" -Force
$exe = (Get-Command pwsh).Source
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
$ok = Invoke-PlaygroundProcess $exe @('-NoProfile','-Command','[Console]::Write("espaço com espaços")') @{} 10 $root
if ($ok.exitCode -ne 0 -or $ok.stdout -cne 'espaço com espaços') { throw 'Output capture failed' }
$bad = Invoke-PlaygroundProcess $exe @('-NoProfile','-Command','[Console]::Error.Write("expected"); exit 7') @{} 10 $root
if ($bad.exitCode -ne 7 -or $bad.stderr -notmatch 'expected') { throw 'Error capture failed' }
$slow = Invoke-PlaygroundProcess $exe @('-NoProfile','-Command','[Threading.Thread]::Sleep(10000)') @{} 1 $root
if (-not $slow.timedOut) { throw 'Timeout was ignored' }
$volume = Invoke-PlaygroundProcess $exe @('-NoProfile','-Command','[Console]::Out.Write("x" * 100000); [Console]::Error.Write("y" * 100000)') @{} 10 $root
if ($volume.stdout.Length -ne 100000 -or $volume.stderr.Length -ne 100000) { throw 'Pipe capture truncated or deadlocked' }
$malformed = Invoke-PlaygroundProcess $exe @('-NoProfile','-Command','[Console]::Write("not-json")') @{} 10 $root
$rejected = $false
try { Assert-PlaygroundSample ($malformed.stdout | ConvertFrom-Json) } catch { $rejected = $true }
if (-not $rejected) { throw 'Malformed output accepted' }
if ($Integration) {
  $output = Join-Path $root ('artifacts/playground-runner-test-' + [guid]::NewGuid().ToString('N'))
  & "$PSScriptRoot/../Measure-Playground.ps1" -Scenario alias-auto,alias-explicit -Iterations 3 -OutputRoot $output
  $report = Get-Content (Get-ChildItem $output -Filter report.json -Recurse).FullName -Raw | ConvertFrom-Json
  if ($report.status -ne 'complete' -or $report.samples.Count -ne 12 -or $report.preparations.Count -ne 2) { throw 'Incorrect sample counts' }
  if (@($report.samples.result.pid | Sort-Object -Unique).Count -ne 12) { throw 'Processes were reused' }
  foreach ($sample in $report.samples) {
    if ($sample.cacheMode -eq 'fresh' -and $sample.cacheBefore.exists) { throw 'Fresh cache contaminated' }
    if ($sample.cacheMode -eq 'reused' -and -not $sample.cacheBefore.exists) { throw 'Cache was not persisted' }
  }
  $rejected = $false
  try { & "$PSScriptRoot/../Measure-Playground.ps1" -PowerShellPath "$output/missing-executable" -Iterations 1 -OutputRoot "$output/failure" }
  catch { $rejected = $true }
  $failure = Get-Content (Get-ChildItem "$output/failure" -Filter report.json -Recurse).FullName -Raw | ConvertFrom-Json
  if (-not $rejected -or $failure.status -ne 'failed' -or $failure.summaries.Count -ne 0) { throw 'Failed run reported valid aggregates' }
}
Write-Host 'Subprocess and timeout checks passed.'
