FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["WarehouseService/WarehouseService.csproj", "WarehouseService/"]
RUN dotnet restore "WarehouseService/WarehouseService.csproj"
COPY . .
WORKDIR "/src/WarehouseService"
RUN dotnet build "WarehouseService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WarehouseService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# TILFØJ DENNE LINJE FOR AT KOPIERE CSV FILER:
COPY WarehouseService/Data/ /app/Data/
HEALTHCHECK --interval=30s --timeout=3s --start-period=40s --retries=3 \
  CMD curl -f http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "WarehouseService.dll"]