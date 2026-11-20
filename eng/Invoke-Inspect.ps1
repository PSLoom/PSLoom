#requires -Version 7.4
<#
.SYNOPSIS
  Runs JetBrains inspectcode on the solution and prints one line per finding.
.PARAMETER SolutionWide
  Enables solution-wide analysis with an isolated cache, to confirm a finding that looks stale.
#>
[CmdletBinding()]
param(
  [string] $Path = (Get-Location).Path,
  [switch] $SolutionWide
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$solution = Get-ChildItem -LiteralPath $Path -Filter *.slnx | Select-Object -First 1
if (-not $solution) {
  throw "No .slnx file found in '$Path'."
}

$outputDirectory = Join-Path $Path 'artifacts/inspect'
$null = New-Item -ItemType Directory -Force -Path $outputDirectory
$report = Join-Path $outputDirectory 'inspectcode.sarif'

$arguments = @('jb', 'inspectcode', $solution.FullName, "--output=$report", '--format=Sarif', '--severity=SUGGESTION')
if ($SolutionWide) {
  $arguments += '--swea'
  $arguments += "--caches-home=$(Join-Path $outputDirectory 'caches')"
}

Push-Location -LiteralPath $Path
try {
  & dotnet @arguments
  if ($LASTEXITCODE -ne 0) {
    throw "inspectcode failed with exit code $LASTEXITCODE."
  }
}
finally {
  Pop-Location
}

$sarif = [System.IO.File]::ReadAllText($report) | ConvertFrom-Json
$results = @($sarif.runs | ForEach-Object { $_.results })
foreach ($result in $results) {
  $location = $result.locations[0].physicalLocation
  '{0}: {1} ({2}:{3})' -f $result.ruleId, $result.message.text, $location.artifactLocation.uri, $location.region.startLine
}

"Findings: $($results.Count)"
