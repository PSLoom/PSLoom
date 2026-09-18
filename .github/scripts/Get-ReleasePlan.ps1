#Requires -Version 7.6
param([ValidateSet('develop','main')][string]$Channel, [string]$ResumeTag)
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/Release.psm1" -Force
$config = Get-Content "$PSScriptRoot/../release.json" -Raw | ConvertFrom-Json
$sha = (Invoke-Checked git @('rev-parse','HEAD')).Trim()
$tags = @(Invoke-Checked git @('tag','--list',"$($config.prefix)/v*"))
$output = [ordered]@{ publish = 'false'; sha = $sha; channel = $Channel; version = ''; tag = ''; resume = 'false' }
if ($ResumeTag) {
  if ($ResumeTag -cnotmatch "^$($config.prefix)/v(0\.\d+\.\d+(?:-alpha\.[1-9]\d*)?)$") { throw 'Invalid recovery tag.' }
  $output.version = $Matches[1]
  $output.tag = $ResumeTag
  $output.sha = (Invoke-Checked git @('rev-parse',"$ResumeTag^{commit}")).Trim()
  $output.channel = if ($output.version -match '-alpha\.') { 'develop' } else { 'main' }
  $output.publish = 'true'
  $output.resume = 'true'
}
else {
  $remoteHead = (Invoke-Checked git @('rev-parse',"origin/$Channel")).Trim()
  if ($sha -ne $remoteHead) { throw 'Only the current branch head can start a release.' }
  $reachable = @(Invoke-Checked git @('tag','--merged','HEAD','--list',"$($config.prefix)/v*"))
  $stable = @($reachable | Where-Object { $_ -match '/v0\.\d+\.\d+$' } |
    Sort-Object { [version]($_ -replace '^.*/v','') } -Descending | Select-Object -First 1)
  $stableVersion = if ($stable.Count) { $stable[0] -replace '^.*/v','' } else { '' }
  $channelTags = @($reachable | Where-Object {
    if ($Channel -eq 'develop') { $_ -match '-alpha\.\d+$' } else { $_ -match '/v0\.\d+\.\d+$' }
  })
  foreach ($tag in $channelTags) {
    if ((Invoke-Checked git @('rev-parse',"$tag^{commit}")).Trim() -eq $sha) {
      throw "This commit already has $tag. Use resume_tag to recover it."
    }
  }
  $range = if ($stable.Count) { "$($stable[0])..HEAD" } else { 'HEAD' }
  $messages = ((Invoke-Checked git @('log','--format=%B%x1e',$range)) -join [Environment]::NewLine) -split [char]0x1e |
    ForEach-Object Trim | Where-Object { $_ }
  $versions = @($tags | ForEach-Object { $_ -replace '^.*/v','' })
  $version = Get-NextRelease -StableVersion $stableVersion -Messages $messages -ExistingVersions $versions -Channel $Channel
  if ($version -and $Channel -eq 'develop' -and $channelTags.Count) {
    $latest = $channelTags | Sort-Object { [System.Management.Automation.SemanticVersion]($_ -replace '^.*/v','') } -Descending | Select-Object -First 1
    $newMessages = ((Invoke-Checked git @('log','--format=%B%x1e',"$latest..HEAD")) -join [Environment]::NewLine) -split [char]0x1e |
      ForEach-Object Trim | Where-Object { $_ }
    if (-not (Get-NextRelease -StableVersion $stableVersion -Messages $newMessages -Channel $Channel)) { $version = $null }
  }
  if ($version) {
    if ($version -in $versions) { throw "Version $version is already reserved." }
    if ($Channel -eq 'main' -and $config.prefix -eq 'reed') {
      [xml]$props = Get-Content Directory.Packages.props -Raw
      if ([string]$props.Project.PropertyGroup.PSLoomVersion -match '-') { throw 'Stable Reed requires a stable SDK.' }
    }
    $output.publish = 'true'; $output.version = $version; $output.tag = "$($config.prefix)/v$version"
  }
}
$output | ConvertTo-Json | Write-Host
if ($env:GITHUB_OUTPUT) { foreach ($key in $output.Keys) { "$key=$($output[$key])" >> $env:GITHUB_OUTPUT } }
