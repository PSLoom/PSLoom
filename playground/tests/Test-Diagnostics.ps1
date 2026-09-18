#Requires -Version 7.6
param([switch]$Probe)
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/../Diagnostics.psm1" -Force
function Assert-Rejected([scriptblock]$Action) {
  $rejected = $false
  try { & $Action } catch { $rejected = $true }
  if (-not $rejected) { throw 'Invalid input accepted.' }
}
if ((Get-PlaygroundMedian @(4, 1, 3, 2)) -ne 2.5) { throw 'Even median' }
if ((Get-PlaygroundMedian @(9, 1, 2)) -ne 2) { throw 'Odd median' }
foreach ($values in @(@(), @(-1), @([double]::NaN), @([double]::PositiveInfinity))) {
  Assert-Rejected { Get-PlaygroundMedian $values }
}
foreach ($scenario in 'alias-auto', 'alias-explicit', 'typical', 'eager') {
  $metrics = switch ($scenario) {
    'alias-auto' { @{ firstMs=1.0; secondMs=0.0; totalMs=1.0 } }
    'alias-explicit' { @{ importMs=1.0; aliasMs=2.0; totalMs=3.0 } }
    default { @{ importMs=1.0; draftMs=2.0; totalMs=3.0 } }
  }
  $sample = @{ schemaVersion=1; scenario=$scenario; pid=1; environment=@{powerShell='7.6.6'};
    metrics=$metrics; loomTimings=@() } | ConvertTo-Json -Depth 10 | ConvertFrom-Json
  Assert-PlaygroundSample $sample
  $sample.metrics.totalMs = -1
  Assert-Rejected { Assert-PlaygroundSample $sample }
  $sample.metrics.totalMs = '3'
  Assert-Rejected { Assert-PlaygroundSample $sample }
  $sample.metrics.totalMs = $null
  Assert-Rejected { Assert-PlaygroundSample $sample }
  if ($Probe) {
    $root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
    $output = & pwsh -NoProfile -NonInteractive -File "$PSScriptRoot/../Probe.ps1" -Scenario $scenario -RepositoryRoot $root
    if ($LASTEXITCODE) { throw "Probe failed: $scenario" }
    $actual = $output | ConvertFrom-Json
    Assert-PlaygroundSample $actual
    if (@($actual.environment.loadedBefore | Where-Object { $null -eq $_ }).Count) { throw 'Null module name' }
    if ($actual.environment.loadedBefore -contains 'Microsoft.PowerShell.Utility') { throw 'Utility preloaded' }
  }
}
Write-Host 'Diagnostic contracts passed.'
