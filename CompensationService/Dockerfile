FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["CompensationService/CompensationService.csproj", "CompensationService/"]
RUN dotnet restore "CompensationService/CompensationService.csproj"
COPY . .
WORKDIR "/src/CompensationService"
RUN dotnet build "CompensationService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CompensationService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .
HEALTHCHECK --interval=30s --timeout=3s --start-period=40s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "CompensationService.dll"]

