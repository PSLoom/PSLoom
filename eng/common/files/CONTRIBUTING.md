# Contributing to {{Title}}

{{Title}} is a PSLoom harness: the module `PSLoom.{{Repo}}` requires the base module `PSLoom` and is built
against the base repository checked out next to this one.

## Requirements

- The .NET SDK pinned in `global.json`.
- PowerShell 7.6 or later on `PATH`. The tests import the built modules in a real `pwsh` process.
- [git-flow-next](https://github.com/gittower/git-flow-next), available as `git flow`.
- The base repository cloned as a sibling of this one, in a folder named exactly `PSLoom`:

  ```text
  workspace/
    PSLoom/
    {{Repo}}/
  ```

`PSLoom.ref` holds the tag or commit of the base this repository is built against. CI checks the base out at
exactly that ref. Locally the base may be ahead of it while you work on both.

The shared scripts live in the base and run without a PowerShell profile. Call them with
`pwsh -NoProfile -File ../PSLoom/eng/<script>`.

## Build and test

```powershell
dotnet tool restore
dotnet build
dotnet test
```

Building this repository builds the base as well. The module is assembled in
`artifacts/modules/PSLoom.{{Repo}}/`; its manifest is generated, and `RequiredModules` names the version of the
base it was compiled against. Edit the template in `src/PSLoom.{{Repo}}/PSLoom.{{Repo}}.psd1`, never the
generated file.

To try the built modules in a clean session:

```powershell
pwsh -NoProfile -File ../PSLoom/eng/Enter-DevShell.ps1
```

## Before you commit

Warnings are errors, including analyzer warnings. After any C# change, run:

```powershell
pwsh -NoProfile -File ../PSLoom/eng/Invoke-Cleanup.ps1
pwsh -NoProfile -File ../PSLoom/eng/Invoke-Inspect.ps1
dotnet test
```

After changing a package or project reference, or after moving `PSLoom.ref`:

```powershell
dotnet restore --force-evaluate
dotnet restore --locked-mode
```

The code style, and what the cleanup does not do for you, are described in the
[contributing guide of the base](https://github.com/{{Owner}}/PSLoom/blob/develop/CONTRIBUTING.md#code-style).

## A change that needs the base

1. Change both repositories side by side. Building this one builds the base.
2. Commit the base on its branch and push it.
3. Write that commit to `PSLoom.ref` and commit it here together with the lock files.

On `develop`, `PSLoom.ref` may hold a commit. A release needs it to hold a published tag of the base.

## Shared files

This file and the others listed in `eng/common/sync.psd1` of the base are copied from the base. Do not edit
them here: change the copy in `eng/common/files` of the base, then run:

```powershell
pwsh -NoProfile -File ../PSLoom/eng/Sync-Common.ps1
```

CI runs the same script with `-Check` and fails when a copy has drifted.

## Commits and branches

- Commits follow [Conventional Commits](https://www.conventionalcommits.org). The changelog is generated
  from them.
- `develop` is the integration branch and `main` holds releases. Work on a `feature/*` branch started with
  `git flow feature start`.

## Releases

Releases are made by the `Prepare release` workflow, started by hand with the version. It refuses to run
while `PSLoom.ref` is not a published tag of the base. Do not edit `CHANGELOG.md` or tag by hand.
