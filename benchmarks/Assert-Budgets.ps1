#Requires -Version 7.6
<#
.SYNOPSIS
  Enforces the BenchmarkDotNet performance budgets of the spec against a benchmark run's JSON export.

.DESCRIPTION
  Reads every *-report-full-compressed.json under the results directory (written by `--exporters json`), finds each budgeted
  benchmark and compares its median with the budget times the tolerance, and its allocation with the allocation budget. A
  budgeted benchmark missing from the results is a failure too: a renamed benchmark must not turn a gate off silently.

.PARAMETER ResultsDirectory
  The BenchmarkDotNet results directory. Default: BenchmarkDotNet.Artifacts/results at the repository root.

.PARAMETER Tolerance
  Multiplier applied to every time budget to absorb machine noise. Default 1.2. Allocation budgets have no tolerance.

.EXAMPLE
  dotnet run -c Release --project benchmarks/PSLoom.Benchmarks -- --filter '*StyleResolve*' '*HookDispatch*' --exporters json
  ./benchmarks/Assert-Budgets.ps1
#>
[CmdletBinding()]
param(
  [string]$ResultsDirectory = (Join-Path $PSScriptRoot '..' 'BenchmarkDotNet.Artifacts' 'results'),
  [double]$Tolerance = 1.2
)

$ErrorActionPreference = 'Stop'

# The spec's budgets, by benchmark full name. MaxBytes $null means the budget says nothing about allocation.
$budgets = @(
  [pscustomobject]@{ Benchmark = 'PSLoom.Benchmarks.Styles.StyleResolveBenchmarks.ResolveExactHit'; Label = 'Style resolve, cache hit'; MaxNanoseconds = 50; MaxBytes = 0 }
  [pscustomobject]@{ Benchmark = 'PSLoom.Benchmarks.Hooks.HookDispatchBenchmarks.DispatchWithoutHandlers'; Label = 'Hook dispatch, no handlers'; MaxNanoseconds = 10; MaxBytes = 0 }
)

$reports = @(Get-ChildItem -Path $ResultsDirectory -Filter '*-report-full-compressed.json' -ErrorAction SilentlyContinue)

if ($reports.Count -eq 0) {
  Write-Error "No BenchmarkDotNet JSON reports under '$ResultsDirectory'; run the benchmarks with --exporters json first." -ErrorAction Continue
  exit 1
}

$measured = @{}
foreach ($report in $reports) {
  foreach ($benchmark in (Get-Content -LiteralPath $report.FullName -Raw | ConvertFrom-Json).Benchmarks) {
    $measured[$benchmark.FullName] = $benchmark
  }
}

function Format-Duration([double]$Nanoseconds) {
  if ($Nanoseconds -ge 1e6) { return '{0:N2} ms' -f ($Nanoseconds / 1e6) }
  if ($Nanoseconds -ge 1e3) { return '{0:N2} µs' -f ($Nanoseconds / 1e3) }
  '{0:N2} ns' -f $Nanoseconds
}

$failed = $false
$rows = foreach ($budget in $budgets) {
  $benchmark = $measured[$budget.Benchmark]

  if ($null -eq $benchmark) {
    $failed = $true
    [pscustomobject]@{ Budget = $budget.Label; Median = 'missing'; Limit = Format-Duration $budget.MaxNanoseconds; Allocated = ''; WithinBudget = $false }
    continue
  }

  $median = [double]$benchmark.Statistics.Median
  $bytes = [long]$benchmark.Memory.BytesAllocatedPerOperation
  $withinTime = $median -le $budget.MaxNanoseconds * $Tolerance
  $withinMemory = $null -eq $budget.MaxBytes -or $bytes -le $budget.MaxBytes

  if (-not ($withinTime -and $withinMemory)) {
    $failed = $true
  }

  [pscustomobject]@{
    Budget = $budget.Label
    Median = Format-Duration $median
    Limit = '{0} (×{1})' -f (Format-Duration $budget.MaxNanoseconds), $Tolerance
    Allocated = if ($null -eq $budget.MaxBytes) { "$bytes B" } else { "$bytes B (max $($budget.MaxBytes))" }
    WithinBudget = $withinTime -and $withinMemory
  }
}

$rows | Format-Table -AutoSize | Out-Host

if ($failed) {
  Write-Error 'Performance budget exceeded.' -ErrorAction Continue
  exit 1
}
