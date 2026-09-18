ARG BASE_IMAGE=archlinux:base@sha256:421f8732de4338c86c204a2af2d62d650f46e3a667744ab8dc5e88853481a4aa
FROM ${BASE_IMAGE}
ENV DOTNET_ROOT=/opt/dotnet PATH=/opt/playground:/opt/dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
ARG ARCH_SNAPSHOT
RUN printf 'Server = https://archive.archlinux.org/repos/%s/$repo/os/$arch\n' "$ARCH_SNAPSHOT" > /etc/pacman.d/mirrorlist \
    && pacman -Syyuu --noconfirm ca-certificates curl jq tar gzip git icu openssl zlib krb5 libunwind \
    && mkdir -p /opt/playground && pacman -Q > /opt/playground/system-packages.txt
COPY playground/environments.json /opt/playground/environments.json
COPY playground/images/install-tools.sh /opt/playground/install-tools.sh
RUN sh /opt/playground/install-tools.sh sdk
ARG PS_VERSION
ARG PS_INSTALLATION
ENV PS_VERSION=${PS_VERSION} PS_INSTALLATION=${PS_INSTALLATION}
RUN sh /opt/playground/install-tools.sh powershell
RUN useradd --create-home --uid 10001 playground && mkdir -p /workspace && chown playground:playground /workspace
COPY --chown=playground:playground . /workspace/
WORKDIR /workspace
USER playground
RUN dotnet build PSLoom.slnx -c Release -p:VersionOverride=0.0.0
RUN /opt/playground/pwsh -NoProfile -NonInteractive -File playground/images/Write-Environment.ps1 -Distribution arch
CMD ["/opt/playground/pwsh", "-NoProfile"]
