@{
  # Paths are relative to the repository root and use forward slashes.

  # Files of every code repository.
  Common = @(
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

  # What each kind of repository gets on top of the common files. The base writes its own CONTRIBUTING.md;
  # eng/common/files/CONTRIBUTING.md is the guide of a harness.
  Kinds = @{
    Base = @()
    Harness = @(
      'CONTRIBUTING.md'
    )
  }

  # Files in which {{Owner}}, {{Repo}} and {{Title}} are replaced with the values of psloom.sync.psd1.
  Templates = @(
    'cliff.toml'
    'CONTRIBUTING.md'
  )
}
