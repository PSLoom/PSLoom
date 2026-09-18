#!/bin/sh
set -eu
manifest=/opt/playground/environments.json
case "$1" in
  sdk)
    url=$(jq -r .sdk.url "$manifest")
    checksum=$(jq -r .sdk.sha512 "$manifest")
    curl --fail --location --retry 3 "$url" --output /tmp/sdk.tar.gz
    printf '%s  %s\n' "$checksum" /tmp/sdk.tar.gz | sha512sum --check --strict
    mkdir -p /opt/dotnet
    tar -xzf /tmp/sdk.tar.gz -C /opt/dotnet
    rm /tmp/sdk.tar.gz
    ;;
  powershell)
    case "$PS_INSTALLATION" in
      binary)
        url=$(jq -r --arg version "$PS_VERSION" '.powershell[$version].url' "$manifest")
        checksum=$(jq -r --arg version "$PS_VERSION" '.powershell[$version].sha256' "$manifest")
        curl --fail --location --retry 3 "$url" --output /tmp/powershell.tar.gz
        printf '%s  %s\n' "$checksum" /tmp/powershell.tar.gz | sha256sum --check --strict
        mkdir -p /opt/powershell
        tar -xzf /tmp/powershell.tar.gz -C /opt/powershell
        chmod +x /opt/powershell/pwsh
        ln -s /opt/powershell/pwsh /opt/playground/pwsh
        rm /tmp/powershell.tar.gz
        ;;
      tool)
        printf '%s\n' '<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>' > /tmp/public-nuget.config
        dotnet tool install PowerShell --version "$PS_VERSION" --tool-path /opt/powershell-tool --configfile /tmp/public-nuget.config
        ln -s /opt/powershell-tool/pwsh /opt/playground/pwsh
        ;;
      *) echo 'Unsupported installation' >&2; exit 1 ;;
    esac
    /opt/playground/pwsh -NoProfile -NonInteractive -Command 'if ($PSVersionTable.PSVersion.ToString() -ne $env:PS_VERSION) { throw "PowerShell version mismatch" }'
    ;;
  *) echo 'Unknown installation phase' >&2; exit 1 ;;
esac
