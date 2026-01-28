# Dockerfile
# Multi-stage build for Octoporty Agent.
# Stage 1: Build React frontend with Node.js
# Stage 2: Build .NET backend with SDK
# Stage 3: Chiseled runtime for minimal attack surface.
# Agent listens on port 17201 for web UI and API.

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
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
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

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Octoporty.Agent.dll"]
