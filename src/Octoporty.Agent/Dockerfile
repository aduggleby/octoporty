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
WORKDIR /src

COPY ["Octoporty.Shared/Octoporty.Shared.csproj", "Octoporty.Shared/"]
COPY ["Octoporty.Agent/Octoporty.Agent.csproj", "Octoporty.Agent/"]
RUN dotnet restore "Octoporty.Agent/Octoporty.Agent.csproj"

COPY Octoporty.Shared/ Octoporty.Shared/
COPY Octoporty.Agent/ Octoporty.Agent/

# Copy frontend build output
COPY --from=frontend /src/Octoporty.Agent/wwwroot /src/Octoporty.Agent/wwwroot

WORKDIR /src/Octoporty.Agent
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Octoporty.Agent.dll"]
