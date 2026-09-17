# PSLoom.TestKit

Shared infrastructure for testing PSLoom harnesses on .NET 10 and PowerShell 7.6.

```xml
<PackageReference Include="PSLoom.TestKit" />
```

Pin its version alongside `PSLoom.Warp`, `PSLoom.Build` and `PSLoom` using `PSLoomVersion` in central package management.

- `PowerShellHost` runs PowerShell in a test host.
- `RepositoryLayout` finds the nearest ancestor containing a `.slnx` solution and locates published modules.
- `ErrorIdConvention` checks the naming convention for structured error identifiers.

Tests requiring the published kernel module also reference `PSLoom` with `GeneratePathProperty="true"` and set
`RequiresKernelModule` to `true` through `PSLoom.Build`.

See the [repository](https://github.com/PSLoom/PSLoom) and [documentation](https://github.com/PSLoom/wiki).
