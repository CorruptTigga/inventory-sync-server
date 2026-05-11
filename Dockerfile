FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY InventorySync.Server/InventorySync.Server.csproj InventorySync.Server/
RUN dotnet restore InventorySync.Server/InventorySync.Server.csproj

COPY InventorySync.Server/ InventorySync.Server/
RUN dotnet publish InventorySync.Server/InventorySync.Server.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "InventorySync.Server.dll"]