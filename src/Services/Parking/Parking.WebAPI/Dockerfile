FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/ParkingSystem.BuildingBlocks.csproj src/BuildingBlocks/
COPY src/Services/Parking/Parking.Domain/Parking.Domain.csproj src/Services/Parking/Parking.Domain/
COPY src/Services/Parking/Parking.Application/Parking.Application.csproj src/Services/Parking/Parking.Application/
COPY src/Services/Parking/Parking.Persistence/Parking.Persistence.csproj src/Services/Parking/Parking.Persistence/
COPY src/Services/Parking/Parking.Infrastructure/Parking.Infrastructure.csproj src/Services/Parking/Parking.Infrastructure/
COPY src/Services/Parking/Parking.WebAPI/Parking.WebAPI.csproj src/Services/Parking/Parking.WebAPI/
RUN dotnet restore src/Services/Parking/Parking.WebAPI/Parking.WebAPI.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Parking/ src/Services/Parking/
RUN dotnet publish src/Services/Parking/Parking.WebAPI/Parking.WebAPI.csproj \
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
ENTRYPOINT ["dotnet", "Parking.WebAPI.dll"]

