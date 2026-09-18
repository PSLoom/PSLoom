# PSLoom.Warp

The compile-time contract for PSLoom harnesses: DSL scopes, verbs, lifecycle services and shared types.

Reference it from a harness using central package version management:

```xml
<PackageReference Include="PSLoom.Warp" PrivateAssets="all" ExcludeAssets="runtime" />
```

Never ship `Warp.dll` in a harness module. At runtime the kernel supplies the single copy, with contract assembly version
`1.0.0.0`. Harnesses target Warp instead of referencing the kernel implementation.

Packages are available from the organization's private GitHub Packages feed; local development uses `build/Pack-Local.ps1`.
See the [repository](https://github.com/PSLoom/PSLoom) and [harness authoring documentation](https://github.com/PSLoom/wiki).
