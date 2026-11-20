#requires -Version 7.4
<#
.SYNOPSIS
  Runs JetBrains cleanupcode on the C# files that git reports as modified, or on the whole solution.
#>
[CmdletBinding()]
param(
  [string] $Path = (Get-Location).Path,
  [switch] $All
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$solution = Get-ChildItem -LiteralPath $Path -Filter *.slnx | Select-Object -First 1
if (-not $solution) {
  throw "No .slnx file found in '$Path'."
}

$arguments = @('jb', 'cleanupcode', $solution.FullName, '--profile=Built-in: Full Cleanup')

if (-not $All) {
  $modified = @(
    git -C $Path status --porcelain=v1 --untracked-files=all |
      ForEach-Object { ($_.Substring(3) -split ' -> ')[-1].Trim('"') } |
      Where-Object { $_ -match '\.(cs|csproj)$' }
  )

  if ($modified.Count -eq 0) {
    Write-Host 'No modified C# files to clean up.'
    return
  }

  $arguments += "--include=$($modified -join ';')"
}

Push-Location -LiteralPath $Path
try {
  & dotnet @arguments
  if ($LASTEXITCODE -ne 0) {
    throw "cleanupcode failed with exit code $LASTEXITCODE."
  }
}
finally {
  Pop-Location
}
