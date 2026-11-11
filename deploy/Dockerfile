# Official multi-architecture Microsoft manifest, verified 2026-11-05.
# .NET 10 LTS on Ubuntu 24.04; production remains opt-in behind deployment gates.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee
USER root
# Use an independent Ubuntu mirror when ubuntu.com DNS is unavailable on runners.
# APT still verifies the Ubuntu archive signatures with the image's trusted keyring.
RUN sed -i -e 's|http://archive.ubuntu.com/ubuntu|https://mirrors.edge.kernel.org/ubuntu|g' \
           -e 's|http://security.ubuntu.com/ubuntu|https://mirrors.edge.kernel.org/ubuntu|g' /etc/apt/sources.list.d/ubuntu.sources \
    && apt-get -o Acquire::Retries=3 -o APT::Update::Error-Mode=any update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown app:app /home/app/.aspnet/DataProtection-Keys
WORKDIR /app
COPY --chown=app:app publish/ ./
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Database__ApplyMigrationsOnStartup=false \
    StorageMigration__Execute=false \
    LegacyImageImport__RecoverPendingJobsOnStartup=false
EXPOSE 8080
USER app
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
    CMD curl --fail --silent --show-error --max-time 3 --header 'X-Forwarded-Proto: https' http://127.0.0.1:8080/health || exit 1
ARG COMMIT_SHA
LABEL org.opencontainers.image.source="https://github.com/StartingWeb/AndersonMultiMarcas" \
      org.opencontainers.image.revision="${COMMIT_SHA}"
ENTRYPOINT ["dotnet", "Project.dll"]
