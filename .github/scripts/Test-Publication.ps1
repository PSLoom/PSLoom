#Requires -Version 7.6
# Runs the real publisher against in-memory GitHub/feed adapters; no network calls.
$ErrorActionPreference = 'Stop'
$config = Get-Content "$PSScriptRoot/../release.json" -Raw | ConvertFrom-Json
$work = Join-Path ([IO.Path]::GetTempPath()) ('publication-test-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory $work
$local = Join-Path $work 'local'
$remote = Join-Path $work 'remote'
$feed = Join-Path $work 'feed'
$null = New-Item -ItemType Directory $local,$remote,$feed
$global:publicationFixture = @{
  sha=('a'*40); checkoutSha=('a'*40); tag=$false; release=$null; pushes=0; failAt=2; remote=$remote; feed=$feed; completed=$false
  missingStatus=403; denyDownloads=$false; tagSha=('a'*40)
}
function global:git {
  $global:LASTEXITCODE = 0
  if ($args[0] -eq 'rev-parse') { return $global:publicationFixture.checkoutSha }
  if ($args[0] -eq 'ls-remote') {
    if ($args[1] -eq '--heads' -or $global:publicationFixture.tag) {
      $sha = if ($args[1] -eq '--heads') { $global:publicationFixture.sha } else { $global:publicationFixture.tagSha }
      return $sha + [char]9 + $args[3]
    }
    return
  }
  if ($args[0] -eq 'push') { $global:publicationFixture.tag = $true }
}
function global:gh {
  $global:LASTEXITCODE = 0
  $state = $global:publicationFixture
  if ($args[0] -eq 'api') {
    if (-not $state.release) { return '[[]]' }
    return '[[' + ($state.release | ConvertTo-Json -Depth 5 -Compress) + ']]'
  }
  switch ($args[1]) {
    create { $state.release = @{tag_name=$args[2]; assets=@(); draft=$true} }
    view { return ($state.release | ConvertTo-Json -Depth 5 -Compress) }
    upload {
      $file = Get-Item $args[3]
      Copy-Item $file.FullName (Join-Path $state.remote $file.Name)
      $state.release.assets += @{name=$file.Name}
    }
    download {
      $destination = $args[([array]::IndexOf($args,'--dir')+1)]
      $patternIndex = [array]::IndexOf($args,'--pattern')
      $files = if ($patternIndex -ge 0) { Get-Item (Join-Path $state.remote $args[$patternIndex+1]) } else { Get-ChildItem $state.remote -File }
      $files | ForEach-Object { Copy-Item $_.FullName (Join-Path $destination $_.Name) }
    }
    edit { $state.completed=$true }
    default { throw "Unexpected gh command: $args" }
  }
}
function global:dotnet {
  $global:LASTEXITCODE = 0
  $state = $global:publicationFixture
  $state.pushes++
  if ($state.pushes -eq $state.failAt) { $global:LASTEXITCODE=1; return 'Simulated interrupted publication' }
  $file = Get-Item $args[2]
  Copy-Item $file.FullName (Join-Path $state.feed $file.Name.ToLowerInvariant())
}
function global:Invoke-RestMethod {
  param($Uri,$Headers)
  return @{resources=@(@{'@type'='PackageBaseAddress/3.0.0'; '@id'='https://fixture.invalid/download/'})}
}
function global:Invoke-WebRequest {
  param($Uri,$Headers,$OutFile)
  $file = Join-Path $global:publicationFixture.feed ([uri]$Uri).Segments[-1]
  if ($global:publicationFixture.denyDownloads -or -not (Test-Path $file)) {
    $response = [Net.Http.HttpResponseMessage]::new([Net.HttpStatusCode]$global:publicationFixture.missingStatus)
    throw [Microsoft.PowerShell.Commands.HttpResponseException]::new('Feed request rejected', $response)
  }
  Copy-Item $file $OutFile
}
try {
  $version = '0.1.0-alpha.1'
  foreach ($id in $config.packages) {
    $zip = [IO.Compression.ZipFile]::Open((Join-Path $local "$id.$version.nupkg"), 'Create')
    try {
      $writer = [IO.StreamWriter]::new($zip.CreateEntry("$id.nuspec").Open())
      try { $writer.Write("<package><metadata><id>$id</id><version>$version</version></metadata></package>") }
      finally { $writer.Dispose() }
    } finally { $zip.Dispose() }
  }
  # Include a second asset/package in Reed's failure scenario by interrupting its first upload.
  if ($config.packages.Count -eq 1) { $global:publicationFixture.failAt=1 }
  & "$PSScriptRoot/New-ReleaseManifest.ps1" -Version $version -Sha $global:publicationFixture.sha -Repository "PSLoom/$($config.prefix -eq 'reed' ? 'Reed' : 'PSLoom')" -Directory $local
  $repository = "PSLoom/$($config.prefix -eq 'reed' ? 'Reed' : 'PSLoom')"
  $failed = $false
  try { & "$PSScriptRoot/Publish-Release.ps1" -Repository $repository -Directory $local }
  catch { if ($_ -notmatch 'dotnet failed') { throw }; $failed=$true }
  if (-not $failed -or $global:publicationFixture.completed) { throw 'Partial publication was not preserved as a draft.' }
  $before = @(Get-ChildItem $feed -File).Count
  $global:publicationFixture.failAt = -1
  $global:publicationFixture.checkoutSha = 'b'*40
  $global:publicationFixture.missingStatus = 404
  $resume = Join-Path $work 'resume'
  & "$PSScriptRoot/Publish-Release.ps1" -Repository $repository -Directory $resume -ResumeTag "$($config.prefix)/v$version"
  if ($config.prefix -eq 'reed' -and -not $global:publicationFixture.completed) { throw 'Recovery did not complete.' }
  if ($config.prefix -eq 'psloom' -and $global:publicationFixture.completed) { throw 'Kernel completed before consumer verification.' }
  $expectedPushes = $config.packages.Count + 1
  if ($global:publicationFixture.pushes -ne $expectedPushes) { throw 'Recovery republished an existing package.' }
  $global:publicationFixture.tagSha = 'c'*40
  $failed=$false
  try { & "$PSScriptRoot/Publish-Release.ps1" -Repository $repository -Directory (Join-Path $work 'wrong-tag') -ResumeTag "$($config.prefix)/v$version" }
  catch { if ($_ -notmatch 'Existing tag points to another commit') { throw }; $failed=$true }
  if (-not $failed) { throw 'Recovery accepted a moved tag.' }
  $global:publicationFixture.tagSha = $global:publicationFixture.sha
  $global:publicationFixture.checkoutSha = $global:publicationFixture.sha
  $global:publicationFixture.denyDownloads = $true
  $global:publicationFixture.missingStatus = 401
  $pushes = $global:publicationFixture.pushes
  $failed=$false
  try { & "$PSScriptRoot/Publish-Release.ps1" -Repository $repository -Directory $local }
  catch { if ([int]$_.Exception.Response.StatusCode -ne 401) { throw }; $failed=$true }
  if (-not $failed -or $global:publicationFixture.pushes -ne $pushes) { throw 'Invalid credentials triggered an upload.' }
  $global:publicationFixture.missingStatus = 403
  $global:publicationFixture.failAt = $global:publicationFixture.pushes + 1
  $failed=$false
  try { & "$PSScriptRoot/Publish-Release.ps1" -Repository $repository -Directory $local }
  catch { if ($_ -notmatch 'dotnet failed') { throw }; $failed=$true }
  if (-not $failed) { throw 'Denied preflight hid an upload failure.' }
  $global:publicationFixture.failAt = -1
  $failed=$false
  try { & "$PSScriptRoot/Publish-Release.ps1" -Repository $repository -Directory $local }
  catch { if ([int]$_.Exception.Response.StatusCode -ne 403) { throw }; $failed=$true }
  if (-not $failed -or $global:publicationFixture.completed) { throw 'Publication succeeded without post-upload verification.' }
  $global:publicationFixture.denyDownloads = $false
  $corrupt = Get-ChildItem $feed -File | Select-Object -First 1
  [IO.File]::WriteAllText($corrupt.FullName,'corrupt package')
  $failed=$false
  try { & "$PSScriptRoot/Publish-Release.ps1" -Repository $repository -Directory $local } catch { $failed=$true }
  if (-not $failed) { throw 'Divergent package was accepted.' }
  Write-Host "Passed denied preflight, publication interruption, recovery with corrected tooling, tag integrity, authentication failures, post-upload verification and corruption rejection ($before packages preserved)."
}
finally {
  foreach ($name in @('git','gh','dotnet','Invoke-RestMethod','Invoke-WebRequest')) { Remove-Item "Function:\global:$name" -ErrorAction SilentlyContinue }
  Remove-Variable publicationFixture -Scope Global
  $resolved=[IO.Path]::GetFullPath($work)
  if ([IO.Path]::GetDirectoryName($resolved) -ne [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetTempPath()) -or
      [IO.Path]::GetFileName($resolved) -notmatch '^publication-test-[0-9a-f]{32}$') { throw 'Unsafe cleanup.' }
  Remove-Item -LiteralPath $resolved -Recurse -Force
}
