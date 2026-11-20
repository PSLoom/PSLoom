#requires -Version 7.4
<#
.SYNOPSIS
  Lists the directories where the modules built in this workspace are assembled.
.DESCRIPTION
  Prints artifacts/modules of the given repository, then of every neighbor repository pinned by a
  <Repository>.ref file in its root. Only the directories that exist are printed.
.PARAMETER Path
  The root of the repository; the current directory when omitted.
#>
[CmdletBinding()]
param(
  [string] $Path = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositories = @($Path) + @(
  Get-ChildItem -LiteralPath $Path -Filter *.ref -File |
    Sort-Object Name |
    ForEach-Object { Join-Path $Path '..' $_.BaseName }
)

$repositories |
  ForEach-Object { [System.IO.Path]::GetFullPath((Join-Path $_ 'artifacts/modules')) } |
  Select-Object -Unique |
  Where-Object { Test-Path -LiteralPath $_ }
