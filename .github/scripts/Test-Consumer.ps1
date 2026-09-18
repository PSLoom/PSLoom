#Requires -Version 7.6
param([string]$PackageDirectory = 'artifacts/packages', [Parameter(Mandatory)][string]$Version,
  [Parameter(Mandatory)][string]$Checkout)
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/Release.psm1" -Force
$source = [IO.Path]::GetFullPath($PackageDirectory)
$consumer = [IO.Path]::GetFullPath($Checkout)
$cache = Join-Path $consumer 'artifacts/integration-cache'
$config = Join-Path $consumer 'artifacts/integration-nuget.config'
$null = New-Item -ItemType Directory -Force (Split-Path $config)
$escaped = [Security.SecurityElement]::Escape($source)
@"
<configuration><packageSources><clear/><add key="local" value="$escaped"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources>
<packageSourceMapping><clear/><packageSource key="local"><package pattern="PSLoom"/><package pattern="PSLoom.*"/></packageSource><packageSource key="nuget.org"><package pattern="*"/></packageSource></packageSourceMapping></configuration>
"@ | Set-Content $config -Encoding utf8
Push-Location $consumer
try {
  $properties = @("-p:PSLoomVersion=$Version", "-p:PSLoomLocalPackageSource=$source", "-p:RestoreConfigFile=$config", "-p:RestorePackagesPath=$cache")
  Invoke-Checked dotnet (@('restore','PSLoom.Reed.slnx') + $properties) | Out-Host
  Invoke-Checked dotnet (@('build','PSLoom.Reed.slnx','-c','Release','--no-restore') + $properties) | Out-Host
  Invoke-Checked dotnet (@('test','--solution','PSLoom.Reed.slnx','-c','Release','--no-build') + $properties) | Out-Host
} finally { Pop-Location }
