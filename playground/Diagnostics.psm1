#Requires -Version 7.6
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Number($Value) {
  if ($null -eq $Value -or $Value -is [string] -or $Value -is [bool] -or
      $Value -isnot [ValueType] -or -not [double]::IsFinite([double]$Value) -or $Value -lt 0) {
    throw 'A measurement must be a finite, nonnegative number.'
  }
}

function Get-PlaygroundMedian([object[]]$Values) {
  if (-not $Values.Count) { throw 'No measurements.' }
  foreach ($value in $Values) { Assert-Number $value }
  $sorted = [double[]]$Values.Clone()
  [Array]::Sort($sorted)
  $middle = [int][math]::Floor($sorted.Length / 2)
  if ($sorted.Length % 2) { return $sorted[$middle] }
  return ($sorted[$middle - 1] + $sorted[$middle]) / 2
}

function Assert-PlaygroundSample([object]$Sample) {
  if ($Sample.schemaVersion -ne 1 -or $Sample.pid -le 0 -or -not $Sample.environment.powerShell) {
    throw 'Invalid sample identity.'
  }
  $required = switch ($Sample.scenario) {
    'alias-auto' { @('firstMs','secondMs','totalMs') }
    'alias-explicit' { @('importMs','aliasMs','totalMs') }
    { $_ -in 'typical','eager' } { @('importMs','draftMs','totalMs') }
    default { throw 'Unknown scenario.' }
  }
  if (@($Sample.metrics.PSObject.Properties).Count -ne $required.Count) { throw 'Unexpected metrics.' }
  foreach ($key in $required) { Assert-Number $Sample.metrics.$key }
  foreach ($row in $Sample.loomTimings) {
    if (-not $row.phase -or -not $row.name) { throw 'Invalid Loom row.' }
    Assert-Number $row.inclusiveMs
    Assert-Number $row.exclusiveMs
  }
}

function Invoke-PlaygroundProcess {
  param([string]$FilePath, [string[]]$Arguments, [hashtable]$Environment,
    [int]$TimeoutSeconds, [string]$WorkingDirectory, [string]$LogPrefix)
  $start = [Diagnostics.ProcessStartInfo]::new($FilePath)
  $start.UseShellExecute = $false
  $start.RedirectStandardOutput = $true
  $start.RedirectStandardError = $true
  $start.StandardOutputEncoding = [Text.Encoding]::UTF8
  $start.StandardErrorEncoding = [Text.Encoding]::UTF8
  $start.WorkingDirectory = $WorkingDirectory
  foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
  foreach ($key in $Environment.Keys) { $start.Environment[$key] = $Environment[$key] }
  $process = [Diagnostics.Process]::new()
  $process.StartInfo = $start
  $outFile = $null
  $errFile = $null
  try {
    $null = $process.Start()
    if ($LogPrefix) {
      $outFile = [IO.File]::Open("$LogPrefix.stdout.log", [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read)
      $errFile = [IO.File]::Open("$LogPrefix.stderr.log", [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::Read)
      $stdout = $process.StandardOutput.BaseStream.CopyToAsync($outFile)
      $stderr = $process.StandardError.BaseStream.CopyToAsync($errFile)
    } else {
      $stdout = $process.StandardOutput.ReadToEndAsync()
      $stderr = $process.StandardError.ReadToEndAsync()
    }
    $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
    if ($timedOut) { $process.Kill($true); $process.WaitForExit() }
    if ($LogPrefix) {
      $stdout.GetAwaiter().GetResult()
      $stderr.GetAwaiter().GetResult()
      $outFile.Dispose(); $outFile = $null
      $errFile.Dispose(); $errFile = $null
      $outText = [IO.File]::ReadAllText("$LogPrefix.stdout.log")
      $errText = [IO.File]::ReadAllText("$LogPrefix.stderr.log")
    } else {
      $outText = $stdout.GetAwaiter().GetResult()
      $errText = $stderr.GetAwaiter().GetResult()
    }
    return [pscustomobject]@{ exitCode=$process.ExitCode; stdout=$outText; stderr=$errText; timedOut=$timedOut }
  } finally {
    if ($outFile) { $outFile.Dispose() }
    if ($errFile) { $errFile.Dispose() }
    $process.Dispose()
  }
}

function Get-PlaygroundCacheState([string]$Path) {
  if (-not [IO.File]::Exists($Path)) { return [pscustomobject]@{exists=$false; bytes=0; sha256=$null} }
  $bytes = [IO.File]::ReadAllBytes($Path)
  return [pscustomobject]@{ exists=$true; bytes=$bytes.Length;
    sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant() }
}

Export-ModuleMember -Function Get-PlaygroundMedian,Assert-PlaygroundSample,Invoke-PlaygroundProcess,Get-PlaygroundCacheState
