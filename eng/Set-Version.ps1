#requires -Version 7.4
<#
.SYNOPSIS
  Writes the version of the repository to Version.props.
.PARAMETER Version
  MAJOR.MINOR.PATCH, optionally followed by a pre-release suffix such as -alpha.1.
.PARAMETER Path
  The root of the repository; the current directory when omitted.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)]
  [string] $Version,

  [string] $Path = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Version -cnotmatch '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?$') {
  throw "'$Version' is not MAJOR.MINOR.PATCH with an optional pre-release suffix."
}

$content = @(
  '<Project>'
  '  <PropertyGroup>'
  "    <Version>$Version</Version>"
  '  </PropertyGroup>'
  '</Project>'
  ''
) -join "`n"

[System.IO.File]::WriteAllText((Join-Path $Path 'Version.props'), $content)
