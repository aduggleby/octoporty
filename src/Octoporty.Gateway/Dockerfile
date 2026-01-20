FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Octoporty.Shared/Octoporty.Shared.csproj", "Octoporty.Shared/"]
COPY ["Octoporty.Gateway/Octoporty.Gateway.csproj", "Octoporty.Gateway/"]
RUN dotnet restore "Octoporty.Gateway/Octoporty.Gateway.csproj"

COPY Octoporty.Shared/ Octoporty.Shared/
COPY Octoporty.Gateway/ Octoporty.Gateway/
WORKDIR /src/Octoporty.Gateway
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Octoporty.Gateway.dll"]
