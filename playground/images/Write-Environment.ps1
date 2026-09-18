param([string]$Distribution)
$ErrorActionPreference = 'Stop'
$manifest = Get-Content /opt/playground/environments.json -Raw | ConvertFrom-Json
$sdk = dotnet --version
if ($LASTEXITCODE -ne 0 -or $sdk -ne $manifest.sdk.version) { throw 'Unexpected SDK.' }
$expectedSdk = (Get-Content /workspace/global.json -Raw | ConvertFrom-Json).sdk.version
if ($sdk -ne $expectedSdk) { throw 'Manifest SDK differs from global.json.' }
$metadata = @{
  distribution=$Distribution; baseImage=$manifest.bases.$Distribution
  archSnapshot=$manifest.archSnapshot; sdk=$sdk; installation=$env:PS_INSTALLATION
  powerShell=$PSVersionTable.PSVersion.ToString(); runtime=[Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
  osRelease=[IO.File]::ReadAllText('/etc/os-release')
  systemPackages=[IO.File]::ReadAllLines('/opt/playground/system-packages.txt')
}
$metadata | ConvertTo-Json -Depth 6 | Set-Content /workspace/playground-environment.json
