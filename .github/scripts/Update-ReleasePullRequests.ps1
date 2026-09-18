#Requires -Version 7.6
param([ValidateSet('dependency','promotion','sync')][string]$Mode,
  [ValidateSet('develop','main')][string]$Channel = 'develop', [string]$Version,
  [Parameter(Mandatory)][string]$Repository)
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/Release.psm1" -Force
$base = if ($Mode -eq 'promotion') { 'main' } elseif ($Mode -eq 'sync') { 'develop' } else { $Channel }
$branch = "automation/$Mode-$base"
Invoke-Checked git @('fetch','origin','main','develop') | Out-Host
$existingPr = @((Invoke-Checked gh @('pr','list','--repo',$Repository,'--head',$branch,'--base',$base,'--state','open','--json','number')) -join [Environment]::NewLine | ConvertFrom-Json)
$remote = @(Invoke-Checked git @('ls-remote','--heads','origin',"refs/heads/$branch"))
$oldHead = if ($remote.Count) { ($remote[0] -split '\s+')[0] } else { '' }
if ($oldHead) {
  Invoke-Checked git @('fetch','origin',$branch) | Out-Host
  if ($Mode -eq 'dependency') {
    [xml]$pending = (Invoke-Checked git @('show',"origin/$($branch):Directory.Packages.props")) -join [Environment]::NewLine
    $pendingVersion = [string]$pending.Project.PropertyGroup.PSLoomVersion
    if ([System.Management.Automation.SemanticVersion]$Version -lt [System.Management.Automation.SemanticVersion]$pendingVersion) {
      Write-Host 'A newer SDK update is already pending.'
      return
    }
  }
  $authors = @(Invoke-Checked git @('log','--format=%ae',"origin/$branch",'--not','origin/main','origin/develop'))
  if ($authors | Where-Object { $_ -ne 'release@psloom.dev' }) { throw "Human edits on $branch; manual review required." }
}
Invoke-Checked git @('config','user.name','psloom-release') | Out-Host
Invoke-Checked git @('config','user.email','release@psloom.dev') | Out-Host
$start = if ($Mode -eq 'promotion') { 'origin/develop' } else { "origin/$base" }
Invoke-Checked git @('checkout','-B',$branch,$start) | Out-Host
if ($Mode -eq 'sync') {
  $previous = if ($Repository -eq 'PSLoom/Reed') { Get-Content Directory.Packages.props -Raw } else { '' }
  Invoke-Checked git @('merge','--no-ff','--no-commit','origin/main') | Out-Host
  if ($previous) {
    [xml]$before = $previous
    [xml]$after = Get-Content Directory.Packages.props -Raw
    $old = [string]$before.Project.PropertyGroup.PSLoomVersion
    $new = [string]$after.Project.PropertyGroup.PSLoomVersion
    if ([System.Management.Automation.SemanticVersion]$old -gt [System.Management.Automation.SemanticVersion]$new) {
      $text = Get-Content Directory.Packages.props -Raw
      $text -replace '(?<=<PSLoomVersion[^>]*>)[^<]+', $old | Set-Content Directory.Packages.props -NoNewline
      Invoke-Checked git @('add','Directory.Packages.props') | Out-Host
    }
  }
  $mergeHead = & git rev-parse --verify MERGE_HEAD 2>$null
  if ($LASTEXITCODE -eq 0) { Invoke-Checked git @('commit','-m','chore: synchronize main into develop') | Out-Host }
  $title = 'chore: synchronize main into develop'
}
elseif ($Repository -eq 'PSLoom/Reed') {
  if ($Mode -eq 'dependency' -and -not (Test-Path Directory.Packages.props)) {
    Write-Host 'The initial stable promotion must populate this branch before dependency updates.'
    return
  }
  [xml]$props = Get-Content Directory.Packages.props -Raw
  $old = [string]$props.Project.PropertyGroup.PSLoomVersion
  if ($Mode -eq 'promotion') {
    $Version = ($old -split '-')[0]
    $release = (Invoke-Checked gh @('release','view',"psloom/v$Version",'--repo','PSLoom/PSLoom','--json','isDraft,isPrerelease')) -join [Environment]::NewLine | ConvertFrom-Json
    if ($release.isDraft -or $release.isPrerelease) { throw 'Stable SDK is not published; promotion is blocked.' }
  } else {
    if ($Version -notmatch '^0\.\d+\.\d+(?:-alpha\.[1-9]\d*)?$') { throw 'Invalid SDK version.' }
    if (($Channel -eq 'main') -eq $Version.Contains('-')) { throw 'SDK channel mismatch.' }
    if ([System.Management.Automation.SemanticVersion]$Version -le [System.Management.Automation.SemanticVersion]$old) { return }
  }
  if ($Version -ne $old) {
    $text = Get-Content Directory.Packages.props -Raw
    $text -replace '(?<=<PSLoomVersion[^>]*>)[^<]+', $Version | Set-Content Directory.Packages.props -NoNewline
    Invoke-Checked git @('add','Directory.Packages.props') | Out-Host
    Invoke-Checked git @('commit','-m',"fix(deps): update PSLoom SDK to $Version") | Out-Host
  }
  $title = if ($Mode -eq 'promotion') { 'chore: promote develop to main' } else { "fix(deps): update PSLoom SDK to $Version" }
}
else { $title = 'chore: promote develop to main' }
$count = [int](Invoke-Checked git @('rev-list','--count',"origin/$base..HEAD"))
if ($count -eq 0) { return }
$args = @('push','origin',"HEAD:refs/heads/$branch")
if ($oldHead) { $args += ('--force-with-lease=refs/heads/{0}:{1}' -f $branch,$oldHead) }
Invoke-Checked git $args | Out-Host
$body = "Automated $Mode update. Required CI must pass. Promotion requires human review and merge."
if (-not $existingPr.Count) {
  Invoke-Checked gh @('pr','create','--repo',$Repository,'--base',$base,'--head',$branch,'--title',$title,'--body',$body) | Out-Host
}
if ($Mode -eq 'dependency') { Invoke-Checked gh @('pr','merge',$branch,'--repo',$Repository,'--auto','--squash') | Out-Host }
