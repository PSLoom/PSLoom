@{
  # Test-only harness. No RequiredModules: test runspaces host the kernel from the test assembly, and requiring the PSLoom
  # module would load a second copy of PSLoom.dll from artifacts.
  RootModule = 'PSLoom.Fixture.dll'
  ModuleVersion = '$version$'
  CompatiblePSEditions = @('Core')
  GUID = '7a3f5c1e-2b8d-4e6f-9a01-c4d5e6f70812'

  Author = 'Bruno Sales'
  Description = 'Test harness for the PSLoom loom host'

  PowerShellVersion = '7.6'

  FunctionsToExport = @()
  VariablesToExport = @()
  AliasesToExport = @()
  CmdletsToExport = @()
}
