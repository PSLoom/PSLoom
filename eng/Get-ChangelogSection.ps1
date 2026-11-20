#requires -Version 7.4
<#
.SYNOPSIS
  Prints the release notes of a version: the body of its section in the changelog, without the title.
.DESCRIPTION
  Exits with 1, printing nothing, when the changelog has no section for the version.
.PARAMETER Version
  The version, without the leading v.
.PARAMETER Path
  The changelog file; CHANGELOG.md in the current directory when omitted.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)]
  [string] $Version,

  [string] $Path = (Join-Path (Get-Location).Path 'CHANGELOG.md')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$lines = [System.IO.File]::ReadAllText($Path) -split "`r?`n"
$title = "## [$Version]"
$start = -1

for ($index = 0; $index -lt $lines.Count; $index++) {
  if ($lines[$index].StartsWith($title, [System.StringComparison]::Ordinal)) {
    $start = $index
    break
  }
}

if ($start -lt 0) {
  exit 1
}

$body = [System.Collections.Generic.List[string]]::new()
for ($index = $start + 1; $index -lt $lines.Count; $index++) {
  if ($lines[$index].StartsWith('## ', [System.StringComparison]::Ordinal)) {
    break
  }

  $body.Add($lines[$index])
}

($body -join "`n").Trim()
