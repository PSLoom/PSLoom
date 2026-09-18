# Linux startup playground

Reproduce PowerShell command discovery and PSLoom startup in isolated Ubuntu and Arch environments. This is a diagnostic tool, not a release pipeline or a promise of identical performance on every machine.

## Requirements

- PowerShell 7.6+ on the host and Git.
- Docker with a working Linux amd64 backend, without CPU emulation.
- Network access for image preparation and public NuGet restore; several GB of disk space.

Run commands from the PSLoom repository root. Images build the local source, including relevant uncommitted changes. Host `bin`, `obj`, `artifacts`, personal configuration and Git credentials are not included. The source manifest records commit, dirty state and hashes of the actual files used.

## Container diagnostics

```powershell
./playground/Invoke-Playground.ps1 -Distribution ubuntu -PowerShellVersion 7.6.4 -Installation tool
./playground/Invoke-Playground.ps1 -Distribution arch -PowerShellVersion 7.6.6 -Installation binary
```

Both commands prepare the selected image and then measure offline. Defaults are Ubuntu, PowerShell 7.6.4, binary installation and ten iterations. Use `-Iterations 1` for a smoke test, not a performance conclusion. `-Mode Build` prepares without measuring. Every invocation verifies the source context; Docker reuses unchanged build layers.

Available combinations: Ubuntu/Arch × PowerShell 7.6.4/7.6.6 × binary/dotnet tool. Run comparisons sequentially. Binary distributions carry their runtime, whereas tools use the installed .NET runtime; compare the recorded versions rather than assuming they match.

```powershell
./playground/Invoke-Playground.ps1 -Mode Shell -Distribution arch -PowerShellVersion 7.6.6
```

Shell mode needs a real terminal. It starts `pwsh -NoProfile` as an unprivileged user, offline. PSLoom and Utility are not explicitly preloaded. Interactive PowerShell/PSReadLine can initialize differently from the noninteractive probes. To inspect the local build manually:

```powershell
$env:PSModulePath = '/workspace/artifacts/modules:' + $env:PSModulePath
Import-Module PSLoom
```

Docker images and build cache remain for reuse. Each created container is removed after its results are exported; a forcibly interrupted host process may leave its named container behind. Inspect `docker ps -a --filter label=psloom.playground=true` before removing any specific leftover. No global prune is performed.

## Native diagnostics

These use the same probe without requiring Docker:

```powershell
dotnet build PSLoom.slnx -c Release
./playground/Measure-Playground.ps1 -Iterations 10
./playground/Measure-Playground.ps1 -Scenario alias-auto,alias-explicit -CacheMode fresh,reused -Iterations 10
```

`-PowerShellPath` selects another executable; otherwise the runner preserves its own PowerShell host, including dotnet-tool hosting. `-TimeoutSeconds` defaults to 60 per subprocess. Child user/cache directories are isolated; the host profile and personal cache are not modified.

## What is measured

| Scenario | Measurements |
|---|---|
| `alias-auto` | First and second `Set-Alias` calls in the same new process |
| `alias-explicit` | Explicit Utility import, alias call, and their combined cost |
| `typical` | PSLoom import, staged draft, internal Loom timings |
| `eager` | PSLoom import, immediate draft, internal Loom timings |

Every sample gets a new process. Utility must not be loaded at the start of the probe. JSON serialization happens after all timing regions. For drafts, Fixture loading remains inside the draft interval, matching the existing benchmark. Time spent launching the PowerShell process is not included.

`fresh` starts with an empty module-analysis cache per sample. `reused` first prepares that scenario in a separate process and waits for PowerShell to persist a cache containing `Set-Alias`; this can take tens of seconds. Preparation is not a measured sample. Each scenario has its own cache. Cache paths, sizes and hashes are recorded. Neither mode resets the kernel's filesystem/page cache.

The `typical` measurement covers the draft window, **not completion of all deferred work or first-prompt responsiveness**. `Measure-Loom` inclusive rows overlap and must not be added together. Its `Total` is recorded before Shed completion and session-starting hooks; the external draft duration also includes other uninstrumented costs.

## Results and failures

Each run creates a unique directory under `artifacts/playground/`. Container runs include build logs, image identity, source context and exported `results/`. Native and container reports contain:

- `report.json`: schema version, environment, preparations, individual samples and medians.
- `summary.txt`: readable comparison by scenario, cache mode and metric.
- Per-process stdout/stderr logs, including failures.

Results include selected runtime/module paths, not a dump of environment variables. Invalid measurements, subprocess errors, timeouts or missing cache persistence fail the run. A failed run retains partial results but emits no aggregate medians. Exceeding a duration budget does not fail this exploratory tool; existing project budgets remain unchanged.

Ubuntu and Arch base digests, SDK downloads and binary checksums are pinned in `environments.json`. Arch system packages come from the recorded archive snapshot; Ubuntu package versions are recorded in image metadata but apt repository updates can change a clean rebuild. Preserve the image ID and inventory when comparing results. The Arch binary/tool setup does not reproduce an arbitrary AUR package.

Containers share their Linux backend kernel and do not replace native Windows/macOS tests. Failure to reproduce a delay is a valid finding, not permission to change the benchmark until it looks fast.

## Tests

```powershell
./playground/tests/Test-Diagnostics.ps1 -Probe
./playground/tests/Test-Runner.ps1 -Integration
./playground/tests/Test-Context.ps1
./playground/tests/Test-Containers.ps1
./playground/tests/Test-Containers.ps1 -Live
```

The first three require a local build for draft probes. `-Live` builds and runs both container distributions with one sample. Tests leave their generated fixtures under `artifacts` for inspection.
