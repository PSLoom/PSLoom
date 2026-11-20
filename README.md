# PSLoom

Add the shell ergonomics PowerShell never shipped with, **one module at a time**. PSLoom is the base of a family of
modules called **harnesses**: each harness adds one capability, such as argument completion or syntax highlighting, and
you install only the ones you want. The base stays out of the way; importing it loads one assembly and does nothing
else until you ask.

> [!NOTE]
> This repository is the starting point of a rewrite. Nothing is published to the PowerShell Gallery yet and no harness
> has been released, so the base has little to show on its own. Until a release is published,
> [build from source](#build-from-source) to try it.

## Requirements

- PowerShell 7.6 or later (`pwsh`). Windows PowerShell 5.1 is not supported.
- For a source build only: the .NET SDK pinned in [`global.json`](global.json).

## Choose what to install

| You want                                            | Module            | Status                                |
| :-------------------------------------------------- | :---------------- | :------------------------------------ |
| The base every harness requires                     | `PSLoom`          | In this repository; not published yet |
| Argument completion from a declarative description  | `PSLoom.Reed`     | Planned; lives in its own repository  |
| Syntax highlighting that knows which commands exist | `PSLoom.Colorway` | Planned; lives in its own repository  |

A harness declares the base as a required module, so installing one harness is meant to be enough: the PowerShell
Gallery brings `PSLoom` along with it. You never need the base on its own unless you are writing a harness.

## Install

Once a release is published, install from the PowerShell Gallery in PowerShell 7:

```powershell
Install-PSResource PSLoom
```

This command does not work yet: no version has been published. Use the [source build](#build-from-source) meanwhile.

## Use it

Import the module and list the harnesses loaded in the session:

```powershell
Import-Module PSLoom
Get-Harness
```

`Get-Harness` prints one entry per harness, with its name and the contract version it was built against. With no
harness imported the list is empty, which is the expected result today.

## Troubleshooting

| What you see                                                         | What to check                                                                                                                      |
| :------------------------------------------------------------------- | :--------------------------------------------------------------------------------------------------------------------------------- |
| `Import-Module` refuses the module because of the PowerShell version | Check `$PSVersionTable.PSVersion`. The module needs PowerShell 7.6 or later.                                                       |
| A harness reports that it was compiled against another contract      | The harness and the base come from incompatible releases. Update the one that is behind; the message names both contract versions. |
| `Get-Harness` prints nothing                                         | No harness was imported in this session. Import one first; the base alone registers none.                                          |
| `Import-Module PSLoom` finds no module after a source build          | A plain session does not look in `artifacts/modules`. Open the session with `eng/Enter-DevShell.ps1`, as shown below.              |

## Technical reference

<details>
<summary>How the base and the harnesses fit together</summary>

### One base, one copy

The base is a single assembly, `PSLoom.dll`. Everything it declares `public` is the **contract** harnesses compile
against; the rest is internal. A harness never ships its own copy of that assembly. It lists `PSLoom` in
`RequiredModules`, so PowerShell imports the base first and the harness binds to the copy already loaded.

### Contract version

The assembly version of the base carries only the contract major. When a harness registers itself, the base compares
the major the harness was compiled against with its own and refuses a mismatch with a message that names both, instead
of failing later with a missing-member error. Before 1.0 the major is 0 and carries no compatibility promise.

### An import that does nothing

Importing the base registers its cmdlets and stops there. It does not load PSReadLine, subscribe to engine events, or
load any other module. Tests import the built module in a separate `pwsh -NoProfile` process and check exactly that,
along with a budget for how long the import may take.

### A generated manifest

The module manifest is written by the build. The version comes from `Version.props` and `CmdletsToExport` from the
compiled assembly, so a cmdlet cannot exist in the code and be missing from the manifest.

</details>

## Build from source

With the .NET SDK pinned in `global.json`, build the solution and open a session that sees the result:

```powershell
dotnet build
pwsh -NoProfile -File eng/Enter-DevShell.ps1
```

The build assembles the module in `artifacts/modules/PSLoom/`. `Enter-DevShell.ps1` opens a PowerShell session without
your profile in which `Import-Module PSLoom` resolves to that build.

Tests, validation scripts, and repository conventions are in [CONTRIBUTING.md](CONTRIBUTING.md).

## Documentation

Guides and the API reference will live in the [PSLoom wiki](https://wiki.psloom.dev). The site has not been deployed
yet.

## Acknowledgments

- [zi](https://github.com/z-shell/zi) and its annexes inspired the split between a small base and capabilities that are
  installed separately.

## License

PSLoom is licensed under the [zlib/libpng License](LICENSE.md).
