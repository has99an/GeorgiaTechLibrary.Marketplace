FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["BookService/BookService.csproj", "BookService/"]
RUN dotnet restore "BookService/BookService.csproj"
COPY . .
WORKDIR "/src/BookService"
RUN dotnet build "BookService.csproj" -c Release -o /app/build
RUN dotnet tool install --global dotnet-ef --version 8.0.0

FROM build AS publish
RUN dotnet publish "BookService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BookService.dll"]