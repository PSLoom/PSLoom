# PSLoom

The shell-ergonomics layer PowerShell never shipped with: declarative profiles, styles, hooks, command shortcuts and staged startup.

PSLoom runs on PowerShell 7.6+ (Core). Its kernel owns the draft lifecycle; harnesses add features through the Warp contract.

## Start with a draft

> [!NOTE]
> The PowerShell Gallery release is pending. Once published, install with `Install-PSResource PSLoom -Scope CurrentUser`.
> For now, build from source and import the module from `artifacts/modules`.

```powershell
Import-Module PSLoom
Invoke-Loom -Draft {
  Style 'prompt:*' 'color' 'Cyan'
  Treadle gst { git status --short }
  Shed -Slot 1a
  Treadle glog { git log --oneline --graph --decorate }
}
```

Styles store preferences by context. Treadles define command shortcuts. Shed defers the next top-level statement to a later startup slot.

[Reed](https://github.com/PSLoom/Reed) adds declarative native-command completion with `Thread Reed` and the `Sley` DSL.
Colorway (highlighting), Weft (plugin management) and Shuttle (binary fetching) are planned harnesses.

## Build locally

Install the .NET SDK pinned in `global.json` and PowerShell 7.6+, then run:

```powershell
git clone --branch develop https://github.com/PSLoom/PSLoom.git
Set-Location PSLoom
dotnet build PSLoom.slnx -c Release
dotnet test --solution PSLoom.slnx -c Release --no-build
$env:PSModulePath = (Join-Path $PWD 'artifacts/modules') + [IO.Path]::PathSeparator + $env:PSModulePath
Import-Module PSLoom
```

The kernel restores public dependencies from nuget.org; a GitHub Packages token is not needed to build it.

```powershell
./benchmarks/Measure-Startup.ps1 -DraftPath ./benchmarks/drafts/typical.ps1 -AllowHarness Fixture
dotnet run -c Release --project benchmarks/PSLoom.Benchmarks -- --filter '*StyleResolveBenchmarks*' '*HookDispatchBenchmarks*' --exporters json
./benchmarks/Assert-Budgets.ps1
```

## Automated releases

Pushes to `develop` validate and prepare automatic `alpha.N` releases; `main` produces stable `0.x` versions. Tags and GitHub Packages publication are managed by the release coordinator after CI and final-package validation. Publication remains disabled until `RELEASE_ENABLED=true` and the GitHub App and package permissions are configured.

Promotion PRs require human merge. SDK update PRs in Reed enable auto-merge after required checks. Performance durations are initially reported without blocking releases; execution failures and invalid results still fail.

See [release operations](.github/RELEASING.md) for setup, dry runs, branch protections, integration tests and recovery of partial publications. Nothing is published to PSGallery or nuget.org.

## Harness SDK

| Package | Purpose |
| --- | --- |
| `PSLoom.Warp` | Compile-time harness contract; the kernel supplies the runtime assembly. |
| `PSLoom.Build` | Versioning, manifest generation, module publishing and kernel restoration targets. |
| `PSLoom.TestKit` | Shared PowerShell host, repository layout and error-id helpers for tests. |
| `PSLoom` | Kernel assembly for hosted tests and the complete PowerShell module layout. |

All four packages share a version and publish together through the release coordinator, recorded by `psloom/v*` tags.
The internal Fixture harness supports kernel tests and is not packaged.

For a local contract change:

```powershell
./build/Pack-Local.ps1
# In a harness checkout, use the exact version printed by the script:
$kernelVersion = '0.0.0-local.20260916120000' # replace with the printed version
dotnet build -p:PSLoomVersion=$kernelVersion
```

Local packages go to `~/.psloom/packages`. Feed consumers need a personal access token with `read:packages` in
`GITHUB_PACKAGES_TOKEN`; CI uses its repository token with package read access.

Read the [documentation](https://github.com/PSLoom/wiki) for guides, references, architecture and harness authoring.
Released under the [MIT license](LICENSE.md).
