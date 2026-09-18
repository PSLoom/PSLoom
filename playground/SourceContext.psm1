#Requires -Version 7.6
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Invoke-SourceGit([string]$Root, [string[]]$Arguments) {
  $result = & git -c "safe.directory=$($Root.Replace('\','/'))" -C $Root @Arguments
  if ($LASTEXITCODE) { throw 'Cannot identify the Git checkout.' }
  return ($result -join "`n")
}
function Get-PlaygroundSource([string]$RepositoryRoot) {
  $root = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($RepositoryRoot))
  $commit = Invoke-SourceGit $root @('rev-parse','HEAD')
  $dirty = -not [string]::IsNullOrEmpty((Invoke-SourceGit $root @('status','--porcelain=v1')))
  $paths = (Invoke-SourceGit $root @('ls-files','-z','--cached','--others','--exclude-standard')).Split([char]0,
    [StringSplitOptions]::RemoveEmptyEntries)
  $files = [Collections.Generic.List[object]]::new()
  foreach ($path in ($paths | Sort-Object -Unique -CaseSensitive)) {
    if ($path -notmatch '^(src/|tests/|benchmarks/|playground/|global\.json$|Directory\.[^/]+\.(props|targets)$|PSLoom\.slnx$|nuget\.config$|\.editorconfig$|LICENSE\.md$)') { continue }
    if ($path -match '(^|/)(bin|obj|artifacts|node_modules|\.git|\.env[^/]*)(/|$)|\.(pem|pfx|key|user|nupkg|snupkg)$') { continue }
    $full = [IO.Path]::GetFullPath([IO.Path]::Combine($root, $path))
    if (-not $full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Source escapes root.' }
    $cursor = $full
    $linked = $false
    while ($cursor -ne $root) {
      if ([IO.File]::Exists($cursor) -or [IO.Directory]::Exists($cursor)) {
        if ([IO.File]::GetAttributes($cursor) -band [IO.FileAttributes]::ReparsePoint) { $linked = $true; break }
      }
      $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
    if ($linked -or -not [IO.File]::Exists($full)) { continue }
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($full))).ToLowerInvariant()
    $files.Add([pscustomobject]@{path=$path; sha256=$hash})
  }
  $canonical = ($files | ForEach-Object { "$($_.path)`0$($_.sha256)" }) -join "`n"
  $sourceHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($canonical))).ToLowerInvariant()
  return [pscustomobject]@{commit=$commit; dirty=$dirty; sourceHash=$sourceHash; files=$files.ToArray()}
}
function New-PlaygroundContext([string]$RepositoryRoot, [string]$Destination) {
  $root = [IO.Path]::GetFullPath($RepositoryRoot)
  $destinationPath = [IO.Path]::GetFullPath($Destination)
  if ([IO.Directory]::Exists($destinationPath) -and [IO.Directory]::GetFileSystemEntries($destinationPath).Length) {
    throw 'Context destination must be empty.'
  }
  $source = Get-PlaygroundSource $root
  $null = [IO.Directory]::CreateDirectory($destinationPath)
  foreach ($file in $source.files) {
    $target = [IO.Path]::Combine($destinationPath, $file.path)
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
    [IO.File]::Copy([IO.Path]::Combine($root, $file.path), $target, $false)
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -cne $file.sha256) { throw 'Source changed while copying.' }
  }
  $after = Get-PlaygroundSource $root
  if ($after.sourceHash -cne $source.sourceHash -or $after.commit -cne $source.commit -or $after.dirty -ne $source.dirty) {
    throw 'Checkout changed during context creation.'
  }
  $source | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath "$destinationPath/source-manifest.json" -Encoding utf8
  return $source
}
Export-ModuleMember -Function Get-PlaygroundSource,New-PlaygroundContext
