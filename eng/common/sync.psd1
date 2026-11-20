@{
  # Paths are relative to the repository root and use forward slashes.
  Kinds = @{
    Base = @(
      '.editorconfig'
      '.gitattributes'
      '.gitignore'
      'dotnet-tools.json'
      'global.json'
      'LICENSE.md'
    )
  }

  # Files in which {{Owner}}, {{Repo}} and {{Title}} are replaced with the values of psloom.sync.psd1.
  Templates = @()
}
