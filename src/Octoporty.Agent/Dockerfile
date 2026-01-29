# Dockerfile
# Multi-stage build for Octoporty Agent.
# Stage 1: Build React frontend with Node.js
# Stage 2: Build .NET backend with SDK
# Stage 3: Runtime image (non-chiseled for UID flexibility)
# Agent listens on port 17201 for web UI and API.
#
# UID Compatibility:
# - Uses regular aspnet image (not chiseled) to support arbitrary UID override
# - TrueNAS SCALE runs containers as UID 568 by default
# - Default is UID 1654 (APP_UID) when no override is specified
# - Chiseled images segfault when run as different UID because files are owned by 1654

# Stage 1: Build React frontend
FROM node:22-alpine AS frontend
WORKDIR /src/Octoporty.Agent.Web
COPY Octoporty.Agent.Web/package*.json ./
RUN npm ci
COPY Octoporty.Agent.Web/ .
# Vite outputs to ../Octoporty.Agent/wwwroot (configured in vite.config.ts)
RUN npm run build

# Stage 2: Build .NET backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG VERSION=0.0.0
WORKDIR /src

COPY ["Octoporty.Shared/Octoporty.Shared.csproj", "Octoporty.Shared/"]
COPY ["Octoporty.Agent/Octoporty.Agent.csproj", "Octoporty.Agent/"]
RUN dotnet restore "Octoporty.Agent/Octoporty.Agent.csproj"

COPY Octoporty.Shared/ Octoporty.Shared/
COPY Octoporty.Agent/ Octoporty.Agent/

# Copy frontend build output
COPY --from=frontend /src/Octoporty.Agent/wwwroot /src/Octoporty.Agent/wwwroot

WORKDIR /src/Octoporty.Agent
RUN dotnet publish -c Release -o /app/publish /p:Version=${VERSION}

# Stage 3: Runtime
# Use regular aspnet image (not chiseled) to support arbitrary UID override via --user flag.
# Chiseled images have files owned by UID 1654 and segfault when run as different UID.
# Regular images have root-owned files (world-readable) that work with any UID.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
ARG VERSION=0.0.0
WORKDIR /app

# Version label for update scripts to read
LABEL org.opencontainers.image.version="${VERSION}"
LABEL org.opencontainers.image.title="Octoporty Agent"
LABEL org.opencontainers.image.description="Self-hosted reverse proxy tunneling solution - Agent component"
LABEL org.opencontainers.image.url="https://octoporty.com"
LABEL org.opencontainers.image.source="https://github.com/aduggleby/octoporty"

# Agent port (17201)
EXPOSE 17201

ENV ASPNETCORE_URLS=http://+:17201
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Files are copied with world-readable permissions so any UID can access them.
# The --chmod flag ensures files/dirs are readable regardless of build stage umask.
COPY --from=build --chmod=755 /app/publish .

# Note: /app/data is NOT pre-created in the image.
# The app creates it at runtime, owned by whatever UID the container runs as.
# This supports both default (UID 1654) and TrueNAS (UID 568 via Security Context).

ENTRYPOINT ["dotnet", "Octoporty.Agent.dll"]
