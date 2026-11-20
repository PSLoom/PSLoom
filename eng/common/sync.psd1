@{
  # Paths are relative to the repository root and use forward slashes.
  Kinds = @{
    Base = @(
      '.editorconfig'
      '.gitattributes'
      '.github/workflows/ci.yml'
      '.github/workflows/prepare-release.yml'
      '.github/workflows/release.yml'
      '.gitignore'
      'cliff.toml'
      'dotnet-tools.json'
      'global.json'
      'LICENSE.md'
    )
  }

  # Files in which {{Owner}}, {{Repo}} and {{Title}} are replaced with the values of psloom.sync.psd1.
  Templates = @(
    'cliff.toml'
  )
}
