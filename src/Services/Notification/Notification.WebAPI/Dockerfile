FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/ParkingSystem.BuildingBlocks.csproj src/BuildingBlocks/
COPY src/BuildingBlocks/ParkingSystem.AspNetCore/ParkingSystem.AspNetCore.csproj src/BuildingBlocks/ParkingSystem.AspNetCore/
COPY contracts/events/ParkingSystem.Contracts.Events/ParkingSystem.Contracts.Events.csproj contracts/events/ParkingSystem.Contracts.Events/
COPY src/Services/Notification/Notification.Domain/Notification.Domain.csproj src/Services/Notification/Notification.Domain/
COPY src/Services/Notification/Notification.Application/Notification.Application.csproj src/Services/Notification/Notification.Application/
COPY src/Services/Notification/Notification.Persistence/Notification.Persistence.csproj src/Services/Notification/Notification.Persistence/
COPY src/Services/Notification/Notification.Infrastructure/Notification.Infrastructure.csproj src/Services/Notification/Notification.Infrastructure/
COPY src/Services/Notification/Notification.WebAPI/Notification.WebAPI.csproj src/Services/Notification/Notification.WebAPI/
RUN dotnet restore src/Services/Notification/Notification.WebAPI/Notification.WebAPI.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY contracts/events/ contracts/events/
COPY src/Services/Notification/ src/Services/Notification/
RUN dotnet publish src/Services/Notification/Notification.WebAPI/Notification.WebAPI.csproj \
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
ENTRYPOINT ["dotnet", "Notification.WebAPI.dll"]

