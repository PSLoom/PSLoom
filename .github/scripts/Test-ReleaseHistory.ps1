#Requires -Version 7.6
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/Release.psm1" -Force
$planner = Join-Path $PSScriptRoot 'Get-ReleasePlan.ps1'
$work = Join-Path ([IO.Path]::GetTempPath()) ('release-history-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory $work
$previousOutput = $env:GITHUB_OUTPUT
function Commit-Fixture([string]$Message) {
  Invoke-Checked git @('-c','user.name=Release tests','-c','user.email=tests@example.invalid','commit','--allow-empty','-m',$Message) | Out-Null
  Invoke-Checked git @('update-ref','refs/remotes/origin/develop','HEAD') | Out-Null
  Invoke-Checked git @('update-ref','refs/remotes/origin/main','HEAD') | Out-Null
}
function Check-Plan([string]$Channel, [string]$Version) {
  $env:GITHUB_OUTPUT = Join-Path $work 'output.txt'
  [IO.File]::WriteAllText($env:GITHUB_OUTPUT, '')
  & $planner -Channel $Channel
  $values = @{}
  Get-Content $env:GITHUB_OUTPUT | ForEach-Object { $pair = $_ -split '=',2; $values[$pair[0]] = $pair[1] }
  if ($values.version -ne $Version) { throw "History: expected '$Version', got '$($values.version)'." }
}
Push-Location $work
try {
  Invoke-Checked git @('init','--quiet','--initial-branch=develop') | Out-Null
  # Configuration follows the planner's own repository, so use its prefix.
  $config = Get-Content "$PSScriptRoot/../release.json" -Raw | ConvertFrom-Json
  # A stable Reed release requires this fixture's stable SDK.
  '<Project><PropertyGroup><PSLoomVersion>0.1.0</PSLoomVersion></PropertyGroup></Project>' | Set-Content Directory.Packages.props
  Commit-Fixture 'feat: initial'
  Check-Plan develop '0.1.0-alpha.1'
  Invoke-Checked git @('tag',"$($config.prefix)/v0.1.0-alpha.1") | Out-Null
  Commit-Fixture 'docs: guide'
  Check-Plan develop ''
  Check-Plan main '0.1.0'
  Invoke-Checked git @('tag',"$($config.prefix)/v0.1.0") | Out-Null
  Commit-Fixture 'fix: repair'
  Check-Plan develop '0.1.1-alpha.1'
  Invoke-Checked git @('tag',"$($config.prefix)/v0.1.1-alpha.1") | Out-Null
  Commit-Fixture 'feat: new behavior'
  Check-Plan develop '0.2.0-alpha.1'
  Check-Plan main '0.2.0'
  Invoke-Checked git @('tag',"$($config.prefix)/v0.2.0-alpha.1") | Out-Null
  $rejected = $false
  try { & $planner -Channel develop } catch { $rejected = $true }
  if (-not $rejected) { throw 'Reserved release did not require recovery.' }
  & $planner -Channel develop -ResumeTag "$($config.prefix)/v0.2.0-alpha.1"
  Commit-Fixture 'docs: another guide'
  Check-Plan develop ''
  Write-Host 'Passed real Git history, promotion, docs-only and recovery scenarios.'
}
finally {
  Pop-Location
  $env:GITHUB_OUTPUT = $previousOutput
  $resolved = [IO.Path]::GetFullPath($work)
  if ([IO.Path]::GetDirectoryName($resolved) -ne [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetTempPath()) -or
      [IO.Path]::GetFileName($resolved) -notmatch '^release-history-[0-9a-f]{32}$') { throw 'Unsafe cleanup.' }
  Remove-Item -LiteralPath $resolved -Recurse -Force
}
