param([string]$Version = '7.6.4')
$ErrorActionPreference = 'Stop'
$destination = Join-Path $env:RUNNER_TEMP 'pinned-pwsh'
$config = Join-Path $env:RUNNER_TEMP 'powershell-nuget.config'
'<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>' | Set-Content $config
dotnet tool install PowerShell --version $Version --tool-path $destination --configfile $config
if ($LASTEXITCODE -ne 0) { throw 'Pinned PowerShell installation failed.' }
$destination >> $env:GITHUB_PATH
