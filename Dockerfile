# =============================================================
# Dockerfile — PlatformAI  (.NET 10 + Angular)
#
# Stage 1 (node-build):    build Angular → dist/fuse/browser
# Stage 2 (dotnet-build):  restore + build .NET 10 solution
# Stage 3 (publish):       dotnet publish → /app/publish
# Stage 4 (final):         ASP.NET runtime only, no SDK/Node
# =============================================================

# ── Stage 1: Angular build ────────────────────────────────────
FROM node:22-alpine AS node-build
WORKDIR /ui

# Copy package files first (layer cache: re-run only on package changes)
COPY platformAI-ui/package.json platformAI-ui/package-lock.json ./
RUN npm ci --prefer-offline --legacy-peer-deps

# Copy rest of Angular source and build for production
COPY platformAI-ui/ ./
RUN npm run build -- --configuration=production

# ── Stage 2: .NET restore + build ────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src

# Copy solution + all .csproj files to leverage layer cache
COPY PlatformAI.sln .
# global.json non viene copiato: vincola la versione SDK in locale,
# ma in Docker la versione è già fissata dall'immagine base.
COPY PlatformAI/PlatformAI.csproj                           PlatformAI/
COPY PlatformAI.Core/PlatformAI.Core.csproj                 PlatformAI.Core/
COPY PlatformAI.Infrastructure/PlatformAI.Infrastructure.csproj PlatformAI.Infrastructure/
COPY PlatformAI.Analytics/PlatformAI.Analytics.csproj       PlatformAI.Analytics/
COPY PlatformAI.ML/PlatformAI.ML.csproj                     PlatformAI.ML/
COPY PlatformAI.NLP/PlatformAI.NLP.csproj                   PlatformAI.NLP/
COPY PlatformAi.Test/PlatformAi.Test.csproj                  PlatformAi.Test/

RUN dotnet restore PlatformAI.sln

# Copy remaining source
COPY PlatformAI/        PlatformAI/
COPY PlatformAI.Core/   PlatformAI.Core/
COPY PlatformAI.Infrastructure/ PlatformAI.Infrastructure/
COPY PlatformAI.Analytics/      PlatformAI.Analytics/
COPY PlatformAI.ML/             PlatformAI.ML/
COPY PlatformAI.NLP/            PlatformAI.NLP/
COPY PlatformAi.Test/           PlatformAi.Test/

# Copy Angular build output into wwwroot before .NET publish
COPY --from=node-build /ui/dist/fuse/browser/ PlatformAI/wwwroot/

RUN dotnet build PlatformAI.sln -c Release --no-restore

# ── Stage 3: Run tests ────────────────────────────────────────
FROM dotnet-build AS test
WORKDIR /src

RUN dotnet test PlatformAi.Test/PlatformAi.Test.csproj \
    -c Release \
    --no-build \
    --logger "trx;LogFileName=test-results.trx" \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=cobertura \
    /p:CoverletOutput=/coverage/coverage.cobertura.xml \
    || true   # Non blocca il build (pipeline raccoglierà il report)

# ── Stage 4: Publish ──────────────────────────────────────────
FROM dotnet-build AS publish
WORKDIR /src

RUN dotnet publish PlatformAI/PlatformAI.csproj \
    -c Release \
    --self-contained false \
    -o /app/publish \
    /p:UseAppHost=false

# ── Stage 5: Final runtime image ──────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Le immagini .NET 8+ includono già l'utente non-root "app" (UID 1654)
# Non serve crearlo manualmente
WORKDIR /app

COPY --chown=app:app --from=publish /app/publish .

USER app

EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check — Azure App Service / AKS usa questo endpoint
HEALTHCHECK --interval=30s --timeout=10s --start-period=20s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "PlatformAI.dll"]
