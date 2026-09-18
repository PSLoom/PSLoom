#Requires -Version 7.6
param([Parameter(Mandatory)][string]$Repository, [string]$Directory = 'artifacts/release',
  [string]$ResumeTag)
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/Release.psm1" -Force
if ($Repository -notin @('PSLoom/PSLoom','PSLoom/Reed')) { throw 'Unexpected repository.' }
if ($ResumeTag) {
  if ($ResumeTag -notmatch '^(psloom|reed)/v0\.\d+\.\d+(?:-alpha\.[1-9]\d*)?$') { throw 'Invalid recovery tag.' }
  $null = New-Item -ItemType Directory -Force $Directory
  if (Get-ChildItem $Directory -Force) { throw 'Recovery directory must be empty.' }
  Invoke-Checked gh @('release','download',$ResumeTag,'--repo',$Repository,'--dir',$Directory) | Out-Host
}
$manifest = Get-Content (Join-Path $Directory 'release-manifest.json') -Raw | ConvertFrom-Json
Assert-ReleaseAssets $Directory $manifest
$config = Get-Content "$PSScriptRoot/../release.json" -Raw | ConvertFrom-Json
if ($manifest.repository -cne $Repository -or $manifest.tag -cne "$($config.prefix)/v$($manifest.version)" -or
    ($ResumeTag -and $manifest.tag -cne $ResumeTag)) { throw 'Manifest repository/tag mismatch.' }
if (@(Compare-Object @($config.packages) @($manifest.packages)).Count) { throw 'Manifest package set mismatch.' }
# Recovery runs the corrected publisher from the dispatch commit, not the old release.
# The remote tag below must still match the immutable manifest commit.
if (-not $ResumeTag -and (Invoke-Checked git @('rev-parse','HEAD')).Trim() -cne $manifest.sha) { throw 'Checkout does not match release commit.' }
$refs = @(Invoke-Checked git @('ls-remote','--tags','origin',"refs/tags/$($manifest.tag)","refs/tags/$($manifest.tag)^{}"))
if (-not $refs.Count) {
  if ($ResumeTag) { throw 'Recovery tag is missing.' }
  $head = (Invoke-Checked git @('ls-remote','--heads','origin',"refs/heads/$($manifest.channel)")) -split '\s+'
  if ($head[0] -cne $manifest.sha) { throw 'Branch advanced during validation; publish the new head instead.' }
  Invoke-Checked git @('-c','user.name=psloom-release','-c','user.email=release@psloom.dev','tag','-a',$manifest.tag,$manifest.sha,'-m',"Release $($manifest.version)") | Out-Host
  Invoke-Checked git @('push','origin',"refs/tags/$($manifest.tag)") | Out-Host
} else {
  $peeled = $refs | Where-Object { $_ -match '\^\{\}$' } | Select-Object -First 1
  if (-not $peeled) { $peeled = $refs[0] }
  if (($peeled -split '\s+')[0] -cne $manifest.sha) { throw 'Existing tag points to another commit.' }
}
# gh slurp handles pagination explicitly for release lookup.
$releases = (Invoke-Checked gh @('api',"repos/$Repository/releases",'--paginate','--slurp')) -join [Environment]::NewLine | ConvertFrom-Json
$release = @($releases | ForEach-Object { $_ } | Where-Object tag_name -CEQ $manifest.tag) | Select-Object -First 1
if (-not $release) {
  $args = @('release','create',$manifest.tag,'--repo',$Repository,'--verify-tag','--draft','--title',"$($config.module) $($manifest.version)",'--generate-notes')
  if ($manifest.channel -eq 'develop') { $args += '--prerelease' }
  Invoke-Checked gh $args | Out-Host
  $release = (Invoke-Checked gh @('release','view',$manifest.tag,'--repo',$Repository,'--json','isDraft,assets')) -join [Environment]::NewLine | ConvertFrom-Json
}
# Never overwrite an existing asset. Its checksum must match before continuing.
foreach ($file in Get-ChildItem $Directory -File) {
  $existing = @($release.assets | Where-Object name -CEQ $file.Name)
  if ($existing.Count) {
    $temp = Join-Path ([IO.Path]::GetTempPath()) ('release-asset-' + [guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory $temp
    Invoke-Checked gh @('release','download',$manifest.tag,'--repo',$Repository,'--pattern',$file.Name,'--dir',$temp) | Out-Host
    if ((Get-FileHash $file.FullName).Hash -cne (Get-FileHash (Join-Path $temp $file.Name)).Hash) { throw "Asset differs: $($file.Name)" }
  } else {
    Invoke-Checked gh @('release','upload',$manifest.tag,$file.FullName,'--repo',$Repository) | Out-Host
  }
}
$credential = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("PSLoom:$env:GH_TOKEN"))
$headers = @{ Authorization = "Basic $credential" }
$index = Invoke-RestMethod 'https://nuget.pkg.github.com/PSLoom/index.json' -Headers $headers
$base = ($index.resources | Where-Object { $_.'@type' -eq 'PackageBaseAddress/3.0.0' } | Select-Object -First 1).'@id'
if (-not $base) { throw 'Feed has no package download endpoint.' }
foreach ($id in $config.packages) {
  $package = Join-Path $Directory "$id.$($manifest.version).nupkg"
  $uri = "$($base.TrimEnd('/'))/$($id.ToLowerInvariant())/$($manifest.version)/$($id.ToLowerInvariant()).$($manifest.version).nupkg"
  $download = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString('N') + '.nupkg')
  $exists = $true
  try { Invoke-WebRequest $uri -Headers $headers -OutFile $download }
  catch {
    $status = [int]$_.Exception.Response.StatusCode
    if ($status -eq 404) { $exists = $false }
    elseif ($status -eq 403) {
      # A denied preflight does not prove absence. Let the authenticated push
      # decide; duplicate versions and permission failures must still fail.
      Write-Warning "Cannot inspect $id before publication (HTTP 403). Attempting upload; successful download and content verification remain required."
      $exists = $false
    }
    else { throw }
  }
  if ($exists) { Assert-PackageEqual $package $download; continue }
  Invoke-Checked dotnet @('nuget','push',$package,'--source','https://nuget.pkg.github.com/PSLoom/index.json','--api-key',$env:GH_TOKEN) | Out-Host
  $verified = $false
  for ($attempt = 0; $attempt -lt 6; $attempt++) {
    try {
      Invoke-WebRequest $uri -Headers $headers -OutFile $download
      $verified = $true
      break
    } catch {
      if ([int]$_.Exception.Response.StatusCode -ne 404) { throw }
      Start-Sleep -Seconds 5
    }
  }
  if (-not $verified) { throw "Package not visible after publication: $id" }
  Assert-PackageEqual $package $download
}
# Keep kernel drafts until the Reed token has successfully probed all SDK packages.
if ($config.prefix -eq 'psloom') {
  Write-Host 'Packages uploaded and verified. Awaiting Reed feed-access verification.'
  return
}
$args = @('release','edit',$manifest.tag,'--repo',$Repository,'--draft=false')
if ($manifest.channel -eq 'develop') { $args += '--latest=false' }
Invoke-Checked gh $args | Out-Host
