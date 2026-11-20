# Contributing to PSLoom

## Requirements

- The .NET SDK pinned in `global.json`.
- PowerShell 7.6 or later on `PATH`. The tests import the built module in a real `pwsh` process.
- [git-flow-next](https://github.com/gittower/git-flow-next), available as `git flow`.

Every script in `eng/` is meant to run without a PowerShell profile. Call them with `pwsh -NoProfile -File`.

## Build and test

```powershell
dotnet tool restore
dotnet build
dotnet test
```

The build assembles the module in `artifacts/modules/PSLoom/`. Its manifest is generated: the version comes
from `Version.props` and the cmdlet list from the compiled assembly. Edit the template in
`src/PSLoom/PSLoom.psd1`, never the generated file.

To run one test class:

```powershell
dotnet run --project tests/PSLoom.Tests -- -class PSLoom.Tests.HarnessRegistryTests
```

To try the built module in a clean session:

```powershell
pwsh -NoProfile -File eng/Enter-DevShell.ps1
```

## Before you commit

Warnings are errors, including analyzer warnings. After any C# change, run:

```powershell
pwsh -NoProfile -File eng/Invoke-Cleanup.ps1
pwsh -NoProfile -File eng/Invoke-Inspect.ps1
dotnet test
```

`Invoke-Inspect.ps1` prints one line per finding. Fix real problems, apply the suggestions that keep the
intent of the code, and suppress a finding only with a `[SuppressMessage]` that carries a concrete
justification.

After changing a package or project reference, refresh the lock files and check them the way CI does:

```powershell
dotnet restore --force-evaluate
dotnet restore --locked-mode
```

## Code style

`.editorconfig` is the source of truth and the cleanup applies it. Three things it does not do for you:

- Do not wrap lines by hand. Write a declaration or a call on one line and let the cleanup wrap what goes
  past the limit.
- When an expression with `??` has to wrap, keep `??` at the end of the first line.
- In an expression-bodied member, put `=>` on the next line, followed by the body. This does not apply to
  lambdas.

```csharp
var name = harness.GetName().Name ??
           throw new ArgumentException("The harness assembly has no name.", nameof(harness));

public static IReadOnlyCollection<HarnessInfo> GetRegistered()
  => [.. Harnesses.Values.OrderBy(harness => harness.Name, StringComparer.OrdinalIgnoreCase)];
```

## The public surface is the contract

Everything `public` in `src/PSLoom` is the contract harnesses compile against, and it is recorded in
`src/PSLoom/PublicAPI.Unshipped.txt`. Adding a public member without recording it fails the build; so does
removing one that was already shipped. Keep new types `internal` unless a harness needs them. To record new
members:

```powershell
dotnet format analyzers src/PSLoom/PSLoom.csproj --diagnostics RS0016
```

`AssemblyVersion` carries only the contract major (`ContractMajor` in `PSLoom.csproj`). Raising it breaks
every harness built for the previous one.

## Shared files

`eng/` holds what every PSLoom repository shares: the MSBuild configuration in `eng/build`, and the files in
`eng/common/files` that are copied to each repository. Edit the copy in `eng/common/files`, then run:

```powershell
pwsh -NoProfile -File eng/Sync-Common.ps1
```

CI runs the same script with `-Check` and fails when a copy has drifted. So does `dotnet test`.

## Commits and branches

- Commits follow [Conventional Commits](https://www.conventionalcommits.org). The changelog is generated
  from them.
- `develop` is the integration branch and `main` holds releases. Work on a `feature/*` branch started with
  `git flow feature start`.

## Releases

Releases are made by the `Prepare release` workflow, started by hand with the version. It writes the
changelog section and `Version.props`, runs CI on the candidate, merges it into `main` and `develop`, tags
it, and starts the `Release` workflow. Do not edit `CHANGELOG.md` or tag by hand.

`Release` packs each module as a `.nupkg`, attaches it to the GitHub release, and publishes that same file to
the PowerShell Gallery. A version with a pre-release suffix stays on GitHub. To build the package locally:

```powershell
dotnet build --configuration Release
pwsh -NoProfile -File eng/New-ModulePackage.ps1
```

The Gallery job is skipped until the repository is set up for it. All three settings are needed:

| Setting                                    | Where                                 | Purpose                                                       |
| :----------------------------------------- | :------------------------------------ | :------------------------------------------------------------ |
| Variable `PUBLISH_PSGALLERY` set to `true` | Repository variables                  | Opens the Gallery job. Without it the job is skipped.         |
| Secret `PSGALLERY_API_KEY`                 | Repository or `psgallery` environment | The API key the job publishes with.                           |
| Environment `psgallery`                    | Repository environments               | Where the job runs; add required reviewers to gate a publish. |

Set `PUBLISH_PSGALLERY` as a repository variable, not as a variable of the environment. A published version
cannot be replaced, only unlisted.
