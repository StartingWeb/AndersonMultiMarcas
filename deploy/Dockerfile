# Official multi-architecture Microsoft manifest, verified 2026-11-05.
# .NET 9 ends support 2026-11-10: upgrade is a prerequisite to production activation.
FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim@sha256:8bbd3f6fcc157792d96f0932107d9d435df814a2c058ce2891a267fe341dea1a
ARG COMMIT_SHA
LABEL org.opencontainers.image.source="https://github.com/StartingWeb/AndersonMultiMarcas" \
      org.opencontainers.image.revision="${COMMIT_SHA}"
USER root
RUN apt-get update \
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
ENTRYPOINT ["dotnet", "Project.dll"]
