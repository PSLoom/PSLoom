# PSLoom kernel

The PowerShell profile kernel: draft execution, harness composition, styles, hooks, treadles and staged apply.

The NuGet package contains the kernel assembly under `lib/` for hosted tests and the PowerShell module under `module/`,
including its manifest and runtime `Warp.dll`. PowerShell Gallery publication is pending.

Harness integration tests and benchmarks consume the module through the build SDK:

```xml
<PropertyGroup>
  <RequiresKernelModule>true</RequiresKernelModule>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="PSLoom" GeneratePathProperty="true" />
</ItemGroup>
```

With `PSLoom.Build` imported, builds restore the module to `artifacts/modules/PSLoom/<numeric-version>/`.
Production harnesses reference only `PSLoom.Warp`.

See the [repository](https://github.com/PSLoom/PSLoom) and [documentation](https://github.com/PSLoom/wiki).
