#Requires -Version 7.6
param(
  [ValidateSet('Build','Measure','Shell')][string]$Mode = 'Measure',
  [ValidateSet('ubuntu','arch')][string]$Distribution = 'ubuntu',
  [ValidateSet('7.6.4','7.6.6')][string]$PowerShellVersion = '7.6.4',
  [ValidateSet('binary','tool')][string]$Installation = 'binary',
  [ValidateRange(1,1000)][int]$Iterations = 10
)
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/SourceContext.psm1" -Force
Import-Module "$PSScriptRoot/Diagnostics.psm1" -Force
$root = [IO.Path]::GetFullPath("$PSScriptRoot/..")
function Invoke-Docker([string[]]$Arguments, [int]$Timeout = 60) {
  $result = Invoke-PlaygroundProcess 'docker' $Arguments @{} $Timeout $root
  if ($result.timedOut -or $result.exitCode -ne 0) { throw "Docker failed: $($Arguments[0])`n$($result.stderr)`n$($result.stdout)" }
  return $result.stdout.Trim()
}
$info = (Invoke-Docker @('info','--format','{{json .}}')) | ConvertFrom-Json
if ($info.OSType -ne 'linux' -or $info.Architecture -notin 'x86_64','amd64') { throw 'A native Linux amd64 Docker backend is required.' }
if ($Mode -eq 'Shell' -and [Console]::IsInputRedirected) { throw 'Shell mode requires an interactive terminal.' }
$manifest = Get-Content "$PSScriptRoot/environments.json" -Raw | ConvertFrom-Json
if ($manifest.sdk.version -ne (Get-Content "$root/global.json" -Raw | ConvertFrom-Json).sdk.version) { throw 'Pinned SDK differs from global.json.' }
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + [guid]::NewGuid().ToString('N')
$work = Join-Path $root "artifacts/playground/$runId"
$context = Join-Path $work 'context'
$source = New-PlaygroundContext $root $context
$imageTag = "psloom-playground:$Distribution-$PowerShellVersion-$Installation-$($source.sourceHash.Substring(0,12))"
$iidFile = Join-Path $work 'image-id.txt'
$buildArgs = @('build','--platform','linux/amd64','--progress','plain','--iidfile',$iidFile,'--tag',$imageTag,
  '--file',"$context/playground/images/$Distribution.Dockerfile",'--build-arg',"BASE_IMAGE=$($manifest.bases.$Distribution)",
  '--build-arg',"ARCH_SNAPSHOT=$($manifest.archSnapshot)",'--build-arg',"PS_VERSION=$PowerShellVersion",
  '--build-arg',"PS_INSTALLATION=$Installation",$context)
Write-Host "Building $Distribution / PowerShell $PowerShellVersion / $Installation"
$build = Invoke-PlaygroundProcess 'docker' $buildArgs @{} 1800 $root -LogPrefix "$work/build"
$build.stdout | Set-Content "$work/build.stdout.log"
$build.stderr | Set-Content "$work/build.stderr.log"
if ($build.timedOut -or $build.exitCode -ne 0) { throw "Build failed; see $work/build.stderr.log" }
$imageId = (Get-Content $iidFile -Raw).Trim()
$metadata = [ordered]@{imageId=$imageId; imageTag=$imageTag; distribution=$Distribution;
  powerShell=$PowerShellVersion; installation=$Installation; source=$source;
  dockerServer=$info.ServerVersion; dockerKernel=$info.KernelVersion; mode=$Mode}
$metadata | ConvertTo-Json -Depth 8 | Set-Content "$work/run.json"
Write-Host "Image: $imageId"
if ($Mode -eq 'Build') { Write-Host "Build metadata: $work/run.json"; return }
$name = 'psloom-playground-' + [guid]::NewGuid().ToString('N')
$created = $false
try {
  $arguments = @('create','--name',$name,'--network','none','--label','psloom.playground=true')
  if ($Mode -eq 'Shell') { $arguments += @('-it') }
  $arguments += @($imageId,'/opt/playground/pwsh','-NoProfile')
  if ($Mode -eq 'Measure') {
    $arguments += @('-NonInteractive','-File','/workspace/playground/Measure-Playground.ps1','-Iterations',"$Iterations")
  }
  $null = Invoke-Docker $arguments
  $created = $true
  if ($Mode -eq 'Shell') {
    & docker start --attach --interactive $name
    if ($LASTEXITCODE) { throw 'Interactive container failed.' }
  } else {
    $run = Invoke-PlaygroundProcess 'docker' @('start','--attach',$name) @{} ([math]::Min(86400,600 + $Iterations * 480)) $root -LogPrefix "$work/run"
    $run.stdout | Set-Content "$work/run.stdout.log"
    $run.stderr | Set-Content "$work/run.stderr.log"
    Write-Host $run.stdout
    if ($run.timedOut) { $null = Invoke-Docker @('stop','--time','2',$name) }
    $state = (Invoke-Docker @('inspect','--format','{{json .State}}',$name)) | ConvertFrom-Json
    # Preserve partial reports before checking the process result.
    $null = Invoke-Docker @('cp',"${name}:/workspace/artifacts/playground", "$work/results")
    if ($run.timedOut -or $run.exitCode -ne 0 -or $state.ExitCode -ne 0) { throw "Diagnostics failed; retained logs and results in $work" }
  }
  Write-Host "Playground output: $work"
} finally {
  if ($created) {
    # Only this exact, newly-created container is removed; images and results remain.
    $null = Invoke-Docker @('rm','--force',$name)
  }
}
