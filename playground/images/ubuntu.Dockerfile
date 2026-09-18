ARG BASE_IMAGE=ubuntu:24.04@sha256:496754492fb28b4d3049432f2ca787449331e23fb14f0dd3fffea86bf5a93eb4
FROM ${BASE_IMAGE}
ENV DOTNET_ROOT=/opt/dotnet PATH=/opt/playground:/opt/dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl jq tar gzip git libicu74 libssl3 zlib1g libgssapi-krb5-2 libunwind8 \
    && mkdir -p /opt/playground && dpkg-query -W > /opt/playground/system-packages.txt
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
RUN /opt/playground/pwsh -NoProfile -NonInteractive -File playground/images/Write-Environment.ps1 -Distribution ubuntu
CMD ["/opt/playground/pwsh", "-NoProfile"]
