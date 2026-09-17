#Requires -Version 7.6
# Uses local bare Git repositories and a fake PR API. No GitHub mutations.
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/Release.psm1" -Force
$scriptPath = Join-Path $PSScriptRoot 'Update-ReleasePullRequests.ps1'
$work = Join-Path ([IO.Path]::GetTempPath()) ('release-pr-test-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory $work
$global:prFixture = @{exists=$false; creates=0; merges=0}
function global:gh {
  $global:LASTEXITCODE=0
  if ($args[0] -eq 'release') { return '{"isDraft":false,"isPrerelease":false}' }
  switch ($args[1]) {
    list { if ($global:prFixture.exists) { return '[{"number":1}]' } else { return '[]' } }
    create { $global:prFixture.exists=$true; $global:prFixture.creates++ }
    merge { $global:prFixture.merges++ }
    default { throw "Unexpected PR operation: $args" }
  }
}
Push-Location $work
try {
  Invoke-Checked git @('init','--bare','--quiet','remote.git') | Out-Null
  Invoke-Checked git @('init','--quiet','--initial-branch=main','checkout') | Out-Null
  Set-Location checkout
  Invoke-Checked git @('config','user.name','Release fixture') | Out-Null
  Invoke-Checked git @('config','user.email','fixture@example.invalid') | Out-Null
  '<Project><PropertyGroup><PSLoomVersion>0.1.0</PSLoomVersion></PropertyGroup></Project>' | Set-Content Directory.Packages.props
  Invoke-Checked git @('add','Directory.Packages.props') | Out-Null
  Invoke-Checked git @('commit','-qm','feat: initial') | Out-Null
  Invoke-Checked git @('remote','add','origin',(Join-Path $work 'remote.git')) | Out-Null
  Invoke-Checked git @('push','-q','origin','main') | Out-Null
  Invoke-Checked git @('checkout','-qb','develop') | Out-Null
  Invoke-Checked git @('push','-q','origin','develop') | Out-Null
  & $scriptPath -Mode dependency -Channel develop -Version '0.2.0-alpha.2' -Repository PSLoom/Reed
  & $scriptPath -Mode dependency -Channel develop -Version '0.2.0-alpha.2' -Repository PSLoom/Reed
  if ($global:prFixture.creates -ne 1) { throw 'Repeated update duplicated the PR.' }
  & $scriptPath -Mode dependency -Channel develop -Version '0.2.0-alpha.1' -Repository PSLoom/Reed
  [xml]$pending = (Invoke-Checked git @('show','origin/automation/dependency-develop:Directory.Packages.props')) -join [Environment]::NewLine
  if ([string]$pending.Project.PropertyGroup.PSLoomVersion -ne '0.2.0-alpha.2') { throw 'Pending SDK was downgraded.' }
  Invoke-Checked git @('checkout','automation/dependency-develop') | Out-Null
  Invoke-Checked git @('-c','user.name=Human','-c','user.email=human@example.invalid','commit','--allow-empty','-m','docs: human edit') | Out-Null
  Invoke-Checked git @('push','-q','origin','automation/dependency-develop') | Out-Null
  $rejected=$false
  try { & $scriptPath -Mode dependency -Channel develop -Version '0.2.0-alpha.3' -Repository PSLoom/Reed } catch { $rejected=$true }
  if (-not $rejected) { throw 'Human edits were overwritten.' }
  # Promotion includes develop changes and chooses a stable SDK without auto-merge.
  Invoke-Checked git @('checkout','develop') | Out-Null
  '<Project><PropertyGroup><PSLoomVersion>0.2.0-alpha.2</PSLoomVersion></PropertyGroup></Project>' | Set-Content Directory.Packages.props
  Invoke-Checked git @('add','Directory.Packages.props') | Out-Null
  Invoke-Checked git @('commit','-qm','feat: prepare next release') | Out-Null
  Invoke-Checked git @('push','-q','origin','develop') | Out-Null
  $mergeCount = $global:prFixture.merges
  $global:prFixture.exists=$false
  & $scriptPath -Mode promotion -Repository PSLoom/Reed
  [xml]$promoted = Get-Content Directory.Packages.props -Raw
  if ([string]$promoted.Project.PropertyGroup.PSLoomVersion -ne '0.2.0') { throw 'Promotion retained an alpha SDK.' }
  if ($global:prFixture.merges -ne $mergeCount) { throw 'Promotion enabled automatic merge.' }
  Invoke-Checked git @('checkout','main') | Out-Null
  'hotfix' | Set-Content hotfix.txt
  Invoke-Checked git @('add','hotfix.txt') | Out-Null
  Invoke-Checked git @('commit','-qm','fix: stable correction') | Out-Null
  Invoke-Checked git @('push','-q','origin','main') | Out-Null
  $global:prFixture.exists=$false
  & $scriptPath -Mode sync -Repository PSLoom/Reed
  [xml]$synced = Get-Content Directory.Packages.props -Raw
  if ([string]$synced.Project.PropertyGroup.PSLoomVersion -ne '0.2.0-alpha.2' -or -not (Test-Path hotfix.txt)) {
    throw 'Synchronization lost the newer SDK or stable correction.'
  }
  Write-Host 'Passed PR reuse, downgrade prevention, human-edit protection, stable promotion and hotfix synchronization.'
}
finally {
  Pop-Location
  Remove-Item Function:\global:gh -ErrorAction SilentlyContinue
  Remove-Variable prFixture -Scope Global
  $resolved=[IO.Path]::GetFullPath($work)
  if ([IO.Path]::GetDirectoryName($resolved) -ne [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetTempPath()) -or
      [IO.Path]::GetFileName($resolved) -notmatch '^release-pr-test-[0-9a-f]{32}$') { throw 'Unsafe cleanup.' }
  Remove-Item -LiteralPath $resolved -Recurse -Force
}
