FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/ParkingSystem.BuildingBlocks.csproj src/BuildingBlocks/
COPY src/BuildingBlocks/ParkingSystem.AspNetCore/ParkingSystem.AspNetCore.csproj src/BuildingBlocks/ParkingSystem.AspNetCore/
COPY src/Services/Booking/Booking.Domain/Booking.Domain.csproj src/Services/Booking/Booking.Domain/
COPY src/Services/Booking/Booking.Application/Booking.Application.csproj src/Services/Booking/Booking.Application/
COPY src/Services/Booking/Booking.Persistence/Booking.Persistence.csproj src/Services/Booking/Booking.Persistence/
COPY src/Services/Booking/Booking.Infrastructure/Booking.Infrastructure.csproj src/Services/Booking/Booking.Infrastructure/
COPY src/Services/Booking/Booking.WebAPI/Booking.WebAPI.csproj src/Services/Booking/Booking.WebAPI/
RUN dotnet restore src/Services/Booking/Booking.WebAPI/Booking.WebAPI.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY contracts/grpc/ contracts/grpc/
COPY src/Services/Booking/ src/Services/Booking/
RUN dotnet publish src/Services/Booking/Booking.WebAPI/Booking.WebAPI.csproj \
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
ENTRYPOINT ["dotnet", "Booking.WebAPI.dll"]

