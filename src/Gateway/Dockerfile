FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/Gateway/ParkingSystem.Gateway.csproj src/Gateway/
RUN dotnet restore src/Gateway/ParkingSystem.Gateway.csproj

COPY src/Gateway/ src/Gateway/
RUN dotnet publish src/Gateway/ParkingSystem.Gateway.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "ParkingSystem.Gateway.dll"]

