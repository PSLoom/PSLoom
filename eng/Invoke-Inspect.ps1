#requires -Version 7.4
<#
.SYNOPSIS
  Runs JetBrains inspectcode on the solution and prints one line per finding in the files of the repository.
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
$all = @($sarif.runs | ForEach-Object { $_.results })

# The solution of a harness includes projects of its neighbor repositories without their tests, so their public
# members look unused from here. Each repository is inspected in its own solution; only its files are reported.
$results = @($all | Where-Object { $_.locations[0].physicalLocation.artifactLocation.uri -notmatch '^\.\./' })
foreach ($result in $results) {
  $location = $result.locations[0].physicalLocation
  '{0}: {1} ({2}:{3})' -f $result.ruleId, $result.message.text, $location.artifactLocation.uri, $location.region.startLine
}

$outside = $all.Count - $results.Count
if ($outside -gt 0) {
  "Left out: $outside finding(s) in neighbor repositories; inspect those in their own solution."
}

"Findings: $($results.Count)"
