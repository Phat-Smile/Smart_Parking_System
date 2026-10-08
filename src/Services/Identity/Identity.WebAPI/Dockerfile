FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/ParkingSystem.BuildingBlocks.csproj src/BuildingBlocks/
COPY src/BuildingBlocks/ParkingSystem.AspNetCore/ParkingSystem.AspNetCore.csproj src/BuildingBlocks/ParkingSystem.AspNetCore/
COPY contracts/events/ParkingSystem.Contracts.Events/ParkingSystem.Contracts.Events.csproj contracts/events/ParkingSystem.Contracts.Events/
COPY src/Services/Identity/Identity.Domain/Identity.Domain.csproj src/Services/Identity/Identity.Domain/
COPY src/Services/Identity/Identity.Application/Identity.Application.csproj src/Services/Identity/Identity.Application/
COPY src/Services/Identity/Identity.Persistence/Identity.Persistence.csproj src/Services/Identity/Identity.Persistence/
COPY src/Services/Identity/Identity.Infrastructure/Identity.Infrastructure.csproj src/Services/Identity/Identity.Infrastructure/
COPY src/Services/Identity/Identity.WebAPI/Identity.WebAPI.csproj src/Services/Identity/Identity.WebAPI/
RUN dotnet restore src/Services/Identity/Identity.WebAPI/Identity.WebAPI.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY contracts/events/ contracts/events/
COPY src/Services/Identity/ src/Services/Identity/
RUN dotnet publish src/Services/Identity/Identity.WebAPI/Identity.WebAPI.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=
EXPOSE 8080 8081
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Identity.WebAPI.dll"]

