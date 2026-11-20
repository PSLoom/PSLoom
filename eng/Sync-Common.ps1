#requires -Version 7.4
<#
.SYNOPSIS
  Copies the files every PSLoom repository shares from eng/common/files into a repository.
.DESCRIPTION
  The repository says what it is in psloom.sync.psd1 (Kind, Owner, Repo, Title). eng/common/sync.psd1 lists the
  files of each kind. Line endings are compared and written as LF.
.PARAMETER Path
  The root of the repository to update; the current directory when omitted.
.PARAMETER Check
  Writes nothing. Prints the files that differ and exits with 1 when there is any.
#>
[CmdletBinding()]
param(
  [string] $Path = (Get-Location).Path,
  [switch] $Check
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Read-Normalized([string] $File) {
  [System.IO.File]::ReadAllText($File).Replace("`r`n", "`n")
}

$commonRoot = Join-Path $PSScriptRoot 'common'
$catalog = Import-PowerShellDataFile -LiteralPath (Join-Path $commonRoot 'sync.psd1')

$configPath = Join-Path $Path 'psloom.sync.psd1'
if (-not (Test-Path -LiteralPath $configPath)) {
  throw "No psloom.sync.psd1 found in '$Path'."
}

$config = Import-PowerShellDataFile -LiteralPath $configPath
foreach ($key in 'Kind', 'Owner', 'Repo', 'Title') {
  if (-not $config.ContainsKey($key)) {
    throw "psloom.sync.psd1 has no '$key'."
  }
}

if (-not $catalog.Kinds.ContainsKey($config.Kind)) {
  throw "psloom.sync.psd1 names the unknown kind '$($config.Kind)'."
}

$drifted = [System.Collections.Generic.List[string]]::new()

foreach ($relative in $catalog.Kinds[$config.Kind]) {
  $expected = Read-Normalized (Join-Path $commonRoot 'files' $relative)
  if ($relative -in $catalog.Templates) {
    $expected = $expected.Replace('{{Owner}}', $config.Owner).Replace('{{Repo}}', $config.Repo).Replace('{{Title}}', $config.Title)
  }

  $target = Join-Path $Path $relative
  $actual = (Test-Path -LiteralPath $target) ? (Read-Normalized $target) : $null
  if ($actual -ceq $expected) {
    continue
  }

  if ($Check) {
    $drifted.Add($relative)
    continue
  }

  $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target)
  [System.IO.File]::WriteAllText($target, $expected)
  Write-Host "Updated $relative"
}

if ($drifted.Count -gt 0) {
  $drifted | ForEach-Object { Write-Host "Out of sync: $_" }
  exit 1
}
