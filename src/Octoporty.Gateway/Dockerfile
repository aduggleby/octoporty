# Dockerfile
# Multi-stage build for Octoporty Gateway.
# Uses .NET 10 SDK for build and chiseled runtime for minimal attack surface.
# Gateway listens on port 17200 for WebSocket and HTTP traffic.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG VERSION=0.0.0
WORKDIR /src

COPY ["Octoporty.Shared/Octoporty.Shared.csproj", "Octoporty.Shared/"]
COPY ["Octoporty.Gateway/Octoporty.Gateway.csproj", "Octoporty.Gateway/"]
RUN dotnet restore "Octoporty.Gateway/Octoporty.Gateway.csproj"

COPY Octoporty.Shared/ Octoporty.Shared/
COPY Octoporty.Gateway/ Octoporty.Gateway/
WORKDIR /src/Octoporty.Gateway
RUN dotnet publish -c Release -o /app/publish /p:Version=${VERSION}

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
ARG VERSION=0.0.0
WORKDIR /app

# Version label for update scripts to read
LABEL org.opencontainers.image.version="${VERSION}"
LABEL org.opencontainers.image.title="Octoporty Gateway"
LABEL org.opencontainers.image.description="Self-hosted reverse proxy tunneling solution - Gateway component"
LABEL org.opencontainers.image.url="https://octoporty.com"
LABEL org.opencontainers.image.source="https://github.com/aduggleby/octoporty"

# Gateway port (17200 range)
EXPOSE 17200

ENV ASPNETCORE_URLS=http://+:17200
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Use --chown to ensure files are readable by the non-root app user in chiseled images
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
ENTRYPOINT ["dotnet", "Octoporty.Gateway.dll"]
