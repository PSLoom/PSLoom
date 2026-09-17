Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-NextRelease {
  param([string]$StableVersion, [string[]]$Messages, [string[]]$ExistingVersions = @(),
    [ValidateSet('develop', 'main')][string]$Channel)
  $level = 0
  foreach ($message in $Messages) {
    if ($message -match '(?m)^[a-z]+(?:\([^)]+\))?!:|^BREAKING[ -]CHANGE:') { $level = 2 }
    elseif ($message -match '^feat(?:\([^)]+\))?:') { $level = [Math]::Max($level, 2) }
    elseif ($message -match '^(fix|perf)(?:\([^)]+\))?:') { $level = [Math]::Max($level, 1) }
  }
  if ($level -eq 0) { return $null }
  if (-not $StableVersion) { $base = '0.1.0' }
  else {
    $v = [version]$StableVersion
    if ($v.Major -ne 0) { throw 'The 1.0 transition requires an explicit policy change.' }
    $base = if ($level -eq 2) { "0.$($v.Minor + 1).0" } else { "0.$($v.Minor).$($v.Build + 1)" }
  }
  if ($Channel -eq 'main') { return $base }
  $counter = 0
  foreach ($existing in $ExistingVersions) {
    if ($existing -match "^$([regex]::Escape($base))-alpha\.([1-9][0-9]*)$") {
      $counter = [Math]::Max($counter, [int]$Matches[1])
    }
  }
  "$base-alpha.$($counter + 1)"
}
function Invoke-Checked {
  param([string]$Command, [string[]]$Arguments)
  $result = & $Command @Arguments
  if ($LASTEXITCODE -ne 0) {
    $exitCode = $LASTEXITCODE
    $result | Out-Host
    throw "$Command failed with exit code $exitCode."
  }
  $result
}
function Get-ZipContent {
  param([string]$Path)
  $zip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Path))
  try {
    $entries = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $zip.Entries) {
      if ($entries.ContainsKey($entry.FullName)) { throw "Duplicate ZIP entry: $($entry.FullName)" }
      $stream = $entry.Open()
      try { $entries.Add($entry.FullName, [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))) }
      finally { $stream.Dispose() }
    }
    return ,$entries
  }
  finally { $zip.Dispose() }
}
function Assert-PackageEqual {
  param([string]$Expected, [string]$Actual)
  if ((Get-FileHash $Expected).Hash -eq (Get-FileHash $Actual).Hash) { return }
  $left = Get-ZipContent $Expected
  $right = Get-ZipContent $Actual
  if ($left.Count -ne $right.Count) { throw 'Published package has different entries.' }
  foreach ($key in $left.Keys) {
    if (-not $right.ContainsKey($key) -or $left[$key] -cne $right[$key]) { throw "Published package differs at $key." }
  }
}
function Assert-ReleaseAssets {
  param([string]$Directory, $Manifest)
  if ($Manifest.schema -ne 1 -or $Manifest.sha -notmatch '^[0-9a-f]{40}$' -or
      $Manifest.version -notmatch '^0\.\d+\.\d+(?:-alpha\.[1-9]\d*)?$') { throw 'Invalid release manifest.' }
  $names = @($Manifest.assets | ForEach-Object name)
  if ($names.Count -ne @($names | Sort-Object -Unique).Count) { throw 'Duplicate assets.' }
  foreach ($asset in $Manifest.assets) {
    if ($asset.name -cne [IO.Path]::GetFileName($asset.name) -or $asset.name -match '[/\\:]') { throw 'Unsafe asset name.' }
    $path = Join-Path $Directory $asset.name
    if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path).Hash -cne $asset.sha256) {
      throw "Missing or corrupt release asset: $($asset.name)"
    }
  }
  foreach ($file in Get-ChildItem -LiteralPath $Directory -File) {
    if ($file.Name -ne 'release-manifest.json' -and $file.Name -cnotin $names) { throw "Unexpected release asset: $($file.Name)" }
  }
}
Export-ModuleMember -Function Get-NextRelease, Invoke-Checked, Get-ZipContent, Assert-PackageEqual, Assert-ReleaseAssets
