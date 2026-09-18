# PSLoom.Build

Shared MSBuild props and targets for PSLoom harnesses. The package generates PowerShell manifests, publishes module layouts,
derives versions from Git tags and restores the kernel module for tests and benchmarks.

Add it to `Directory.Packages.props`:

```xml
<GlobalPackageReference Include="PSLoom.Build" Version="$(PSLoomVersion)" />
```

| Property | Purpose |
| --- | --- |
| `VersionTagPrefix` | Selects tags such as `reed/v0.1.0-alpha.1` using the prefix `reed`. |
| `VersionOverride` | Explicit package version, used by the local packaging loop. |
| `PowerShellManifest` | Project-relative `.psd1` template containing the `$version$` token. |
| `RequiresKernelModule` | Restores the kernel module before building a consuming project. |
| `ArtifactsModulesDirectory` | Destination root for published and restored PowerShell modules. |

Projects restoring the kernel also reference `PSLoom` with `GeneratePathProperty="true"`. The numeric version goes into the
manifest and module directory; prerelease labels remain in the NuGet package version. `ContractAssemblyVersion` keeps a
contract's runtime binding version stable.

The kernel imports these targets directly from source. See the [repository](https://github.com/PSLoom/PSLoom) and
[harness authoring documentation](https://github.com/PSLoom/wiki).
