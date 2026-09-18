#Requires -Version 7.6
# Exercise the hosting mode used by the pinned .NET tool, even when the test
# itself starts through a native pwsh executable.
$ErrorActionPreference = 'Stop'
$config = Get-Content "$PSScriptRoot/../release.json" -Raw | ConvertFrom-Json
$benchmark = Join-Path $PSScriptRoot '../../benchmarks/Measure-Startup.ps1'
$draft = Join-Path $PSScriptRoot '../../benchmarks/drafts/typical.ps1'
$arguments = @((Join-Path $PSHOME 'pwsh.dll'), '-NoProfile', '-NonInteractive',
  '-File', $benchmark, '-DraftPath', $draft, '-Iterations', '1', '-ReportOnly')
if ($config.prefix -eq 'psloom') { $arguments += @('-AllowHarness', 'Fixture') }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw "Startup measurement failed under the dotnet host: $LASTEXITCODE" }
Write-Host 'Startup measurement passed under the dotnet host.'
