#Requires -Version 7.6
<#
.SYNOPSIS
  Measures cold-process startup cost of the published PSLoom modules and enforces the startup budgets.

.DESCRIPTION
  Starts a fresh `pwsh -NoProfile -NonInteractive` process per iteration against artifacts/modules, times
  `Import-Module PSLoom` (and, when -DraftPath is given, the draft on top of the import) inside that process, and
  reports the median. Exits with code 1 when a median exceeds its budget.

.PARAMETER Iterations
  Number of cold processes per measurement. Default 10.

.PARAMETER ImportBudgetMilliseconds
  Budget for the median `Import-Module PSLoom` time. Default 50.

.PARAMETER DraftPath
  Optional script file containing an `Invoke-Loom -Draft { ... }` call to time after the import.

.PARAMETER DraftBudgetMilliseconds
  Budget for the median draft time over the import. Default 100.

.PARAMETER Tolerance
  Multiplier applied to every budget to absorb machine noise. Default 1.2.
#>
[CmdletBinding()]
param(
  [ValidateRange(1, 1000)][int]$Iterations = 10,
  [double]$ImportBudgetMilliseconds = 50,
  [string]$DraftPath,
  [double]$DraftBudgetMilliseconds = 100,
  [double]$Tolerance = 1.2
)

$ErrorActionPreference = 'Stop'

$modulesDirectory = Join-Path $PSScriptRoot '..' 'artifacts' 'modules' | Resolve-Path
$pwsh = (Get-Process -Id $PID).Path

$probe = @'
param($ModulesDirectory, $DraftPath)
$env:PSModulePath = $ModulesDirectory + [IO.Path]::PathSeparator + $env:PSModulePath
$import = [Diagnostics.Stopwatch]::StartNew()
Import-Module PSLoom
$import.Stop()
$draft = 0.0
if ($DraftPath) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  . $DraftPath
  $sw.Stop()
  $draft = $sw.Elapsed.TotalMilliseconds
}
'{0};{1}' -f $import.Elapsed.TotalMilliseconds.ToString([cultureinfo]::InvariantCulture), $draft.ToString([cultureinfo]::InvariantCulture)
'@

function Get-Median([double[]]$Values) {
  $sorted = $Values | Sort-Object
  $middle = [math]::Floor($sorted.Count / 2)
  if ($sorted.Count % 2) { $sorted[$middle] } else { ($sorted[$middle - 1] + $sorted[$middle]) / 2 }
}

$encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes(
    "& { $probe } -ModulesDirectory '$modulesDirectory' -DraftPath '$DraftPath'"))

$imports = [Collections.Generic.List[double]]::new()
$drafts = [Collections.Generic.List[double]]::new()

for ($i = 0; $i -lt $Iterations; $i++) {
  $output = & $pwsh -NoProfile -NonInteractive -EncodedCommand $encoded
  if ($LASTEXITCODE -ne 0) { throw "Probe process failed with exit code $LASTEXITCODE." }
  $parts = ($output | Select-Object -Last 1) -split ';'
  $imports.Add([double]::Parse($parts[0], [cultureinfo]::InvariantCulture))
  $drafts.Add([double]::Parse($parts[1], [cultureinfo]::InvariantCulture))
}

$failed = $false
$results = @(
  [pscustomobject]@{ Metric = 'Import-Module PSLoom'; MedianMs = Get-Median $imports; BudgetMs = $ImportBudgetMilliseconds }
)
if ($DraftPath) {
  $results += [pscustomobject]@{ Metric = 'Draft'; MedianMs = Get-Median $drafts; BudgetMs = $DraftBudgetMilliseconds }
}

foreach ($result in $results) {
  $result | Add-Member -NotePropertyName WithinBudget -NotePropertyValue ($result.MedianMs -le $result.BudgetMs * $Tolerance)
  if (-not $result.WithinBudget) { $failed = $true }
}

$results | Format-Table -AutoSize | Out-Host

if ($failed) {
  Write-Error 'Startup budget exceeded.' -ErrorAction Continue
  exit 1
}
