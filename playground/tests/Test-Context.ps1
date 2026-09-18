#Requires -Version 7.6
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/../SourceContext.psm1" -Force
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
$first = Get-PlaygroundSource $root
$second = Get-PlaygroundSource $root
if ($first.sourceHash -cne $second.sourceHash) { throw 'Unstable source hash' }
if (-not ($first.files.path -contains 'global.json')) { throw 'Build metadata missing' }
foreach ($file in $first.files) {
  if ($file.path -match '(^|/)(bin|obj|artifacts|\.git)(/|$)|\.(pem|pfx|key)$') { throw 'Excluded file included' }
}
$destination = Join-Path $root ('artifacts/playground-context-test-' + [guid]::NewGuid().ToString('N'))
$copy = New-PlaygroundContext $root $destination
if ($copy.sourceHash -cne $first.sourceHash) { throw 'Context identity differs' }
if (-not (Test-Path "$destination/source-manifest.json")) { throw 'Manifest missing' }
$rejected = $false
try { New-PlaygroundContext $root $destination } catch { $rejected = $true }
if (-not $rejected) { throw 'Nonempty destination accepted' }
Write-Host "Source context passed; inspect $destination"
$fixture = Join-Path $root ('artifacts/context fixture ' + [guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory("$fixture/src/bin")
$null = [IO.Directory]::CreateDirectory("$fixture/artifacts")
foreach ($entry in @{'src/a.cs'='class A {}'; 'src/bin/generated.cs'='generated';
    'src/private.pem'='not-a-real-key'; 'src/.env'='NOT_A_SECRET=fixture';
    'global.json'='{}'; 'artifacts/build.cs'='generated'}.GetEnumerator()) {
  [IO.File]::WriteAllText((Join-Path $fixture $entry.Key), $entry.Value)
}
& git -c "safe.directory=$fixture" -C $fixture init --quiet
& git -c "safe.directory=$fixture" -C $fixture add --all
& git -c "safe.directory=$fixture" -C $fixture -c user.name=Fixture -c user.email=fixture@example.invalid commit --quiet -m 'test: context fixture'
if ($LASTEXITCODE) { throw 'Fixture initialization failed' }
$initial = Get-PlaygroundSource $fixture
if ($initial.files.Count -ne 2 -or $initial.dirty) { throw 'Fixture exclusions or Git state failed' }
[IO.File]::WriteAllText("$fixture/src/a.cs", 'class Changed {}')
$changed = Get-PlaygroundSource $fixture
if ($initial.sourceHash -ceq $changed.sourceHash -or -not $changed.dirty) { throw 'Source edit not reflected' }
[IO.File]::WriteAllText("$fixture/src/bin/generated.cs", 'another generated file')
if ((Get-PlaygroundSource $fixture).sourceHash -cne $changed.sourceHash) { throw 'Build output affected identity' }
$null = New-PlaygroundContext $fixture "$fixture/artifacts/copied context"
Write-Host 'Fixture exclusions, dirty state, hash sensitivity and paths with spaces passed.'
