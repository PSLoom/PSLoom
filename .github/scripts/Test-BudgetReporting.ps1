#Requires -Version 7.6
$ErrorActionPreference = 'Stop'
$config = Get-Content "$PSScriptRoot/../release.json" -Raw | ConvertFrom-Json
$checker = Join-Path $PSScriptRoot '../../benchmarks/Assert-Budgets.ps1'
$work = Join-Path ([IO.Path]::GetTempPath()) ('budget-test-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory $work
$names = if ($config.prefix -eq 'psloom') {
  @('PSLoom.Benchmarks.Styles.StyleResolveBenchmarks.ResolveExactHit','PSLoom.Benchmarks.Hooks.HookDispatchBenchmarks.DispatchWithoutHandlers')
} else {
  @('PSLoom.Benchmarks.Completion.CompletionBenchmarks.Subcommand','PSLoom.Benchmarks.Completion.CompletionBenchmarks.NestedSubcommand','PSLoom.Benchmarks.Completion.CompletionBenchmarks.Option')
}
function Test-Outcome([bool]$ReportOnly,[bool]$ExpectedSuccess) {
  $arguments = @('-NoProfile','-File',$checker,'-ResultsDirectory',$work)
  if ($ReportOnly) { $arguments += '-ReportOnly' }
  $log = & pwsh @arguments 2>&1
  if (($LASTEXITCODE -eq 0) -ne $ExpectedSuccess) { $log | Out-Host; throw 'Unexpected benchmark gate result.' }
}
try {
  $report = @{Benchmarks=@($names | ForEach-Object {
    @{FullName=$_; Statistics=@{Median=1e12}; Memory=@{BytesAllocatedPerOperation=0}}
  })}
  $path = Join-Path $work 'fixture-report-full-compressed.json'
  $report | ConvertTo-Json -Depth 6 | Set-Content $path
  Test-Outcome $true $true
  Test-Outcome $false $false
  $report.Benchmarks[0].Statistics.Median=$null
  $report | ConvertTo-Json -Depth 6 | Set-Content $path
  Test-Outcome $true $false
  $report.Benchmarks=@()
  $report | ConvertTo-Json -Depth 6 | Set-Content $path
  Test-Outcome $true $false
  Write-Host 'Passed informational duration, strict duration, invalid statistics and missing benchmark checks.'
}
finally {
  $resolved=[IO.Path]::GetFullPath($work)
  if ([IO.Path]::GetDirectoryName($resolved) -ne [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetTempPath()) -or
      [IO.Path]::GetFileName($resolved) -notmatch '^budget-test-[0-9a-f]{32}$') { throw 'Unsafe cleanup.' }
  Remove-Item -LiteralPath $resolved -Recurse -Force
}
