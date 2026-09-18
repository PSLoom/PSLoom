#Requires -Version 7.6
param([Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/Release.psm1" -Force
$correlation = [guid]::NewGuid().ToString('N')
Invoke-Checked gh @('workflow','run','feed-access.yml','--repo','PSLoom/Reed','--ref','develop','-f',"version=$Version",'-f',"correlation=$correlation") | Out-Host
for ($attempt=0; $attempt -lt 60; $attempt++) {
  $runs = (Invoke-Checked gh @('run','list','--repo','PSLoom/Reed','--workflow','feed-access.yml','--event','workflow_dispatch','--limit','30','--json','databaseId,displayTitle,status,conclusion')) -join [Environment]::NewLine | ConvertFrom-Json
  $run = $runs | Where-Object displayTitle -CEQ "Feed access $correlation" | Select-Object -First 1
  if ($run -and $run.status -eq 'completed') {
    if ($run.conclusion -ne 'success') { throw "Reed cannot consume SDK $Version; inspect run $($run.databaseId)." }
    return
  }
  Start-Sleep -Seconds 10
}
throw 'Consumer feed probe timed out; release remains a draft.'
