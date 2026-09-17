#Requires -Version 7.6
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/Release.psm1" -Force
$cases = @(
  @{ Stable=''; Messages=@('feat: initial'); Existing=@(); Channel='develop'; Expected='0.1.0-alpha.1' }
  @{ Stable='0.2.0'; Messages=@('fix: repair'); Existing=@(); Channel='develop'; Expected='0.2.1-alpha.1' }
  @{ Stable='0.2.0'; Messages=@('feat!: contract'); Existing=@(); Channel='main'; Expected='0.3.0' }
  @{ Stable='0.2.0'; Messages=@('fix: repair'); Existing=@('0.2.1-alpha.1','0.2.1-alpha.9'); Channel='develop'; Expected='0.2.1-alpha.10' }
  @{ Stable='0.2.0'; Messages=@('fix: repair','feat: add'); Existing=@('0.2.1-alpha.9'); Channel='develop'; Expected='0.3.0-alpha.1' }
  @{ Stable='0.2.0'; Messages=@('docs: guide','ci: validation'); Existing=@(); Channel='develop'; Expected=$null }
  @{ Stable='0.2.0'; Messages=@('fix(deps): update SDK'); Existing=@(); Channel='main'; Expected='0.2.1' }
  @{ Stable='0.2.0'; Messages=@(('refactor: contract' + [Environment]::NewLine + 'BREAKING CHANGE: remove')); Existing=@(); Channel='main'; Expected='0.3.0' }
)
foreach ($case in $cases) {
  $actual = Get-NextRelease -StableVersion $case.Stable -Messages $case.Messages -ExistingVersions $case.Existing -Channel $case.Channel
  if ($actual -ne $case.Expected) { throw "Expected '$($case.Expected)', got '$actual'." }
}
$temp = Join-Path ([IO.Path]::GetTempPath()) ('release-tests-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory $temp
try {
  foreach ($name in @('a','b','c')) {
    $zip = [IO.Compression.ZipFile]::Open((Join-Path $temp "$name.zip"), 'Create')
    try {
      $entry = $zip.CreateEntry('module/test.txt')
      $writer = [IO.StreamWriter]::new($entry.Open())
      try { $writer.Write($(if ($name -eq 'c') { 'corrupt' } else { 'expected' })) } finally { $writer.Dispose() }
    } finally { $zip.Dispose() }
  }
  Assert-PackageEqual (Join-Path $temp 'a.zip') (Join-Path $temp 'b.zip')
  $rejected = $false
  try { Assert-PackageEqual (Join-Path $temp 'a.zip') (Join-Path $temp 'c.zip') } catch { $rejected = $true }
  if (-not $rejected) { throw 'Changed package accepted.' }
  $manifest = [pscustomobject]@{ schema=1; sha=('a' * 40); version='0.1.0-alpha.1'; assets=@(
    Get-ChildItem $temp -File | ForEach-Object { @{name=$_.Name; sha256=(Get-FileHash $_.FullName).Hash} }
  ) }
  Assert-ReleaseAssets $temp $manifest
  $manifest.assets[0].sha256 = 'bad'
  $rejected = $false
  try { Assert-ReleaseAssets $temp $manifest } catch { $rejected = $true }
  if (-not $rejected) { throw 'Corrupt manifest accepted.' }
}
finally {
  $resolved = [IO.Path]::GetFullPath($temp)
  if ([IO.Path]::GetDirectoryName($resolved) -ne [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetTempPath()) -or
      [IO.Path]::GetFileName($resolved) -notmatch '^release-tests-[0-9a-f]{32}$') { throw 'Unsafe cleanup.' }
  Remove-Item -LiteralPath $resolved -Recurse -Force
}
Write-Host "Passed $($cases.Count) version scenarios and package integrity checks."
