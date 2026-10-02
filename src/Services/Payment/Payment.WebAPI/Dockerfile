FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/ParkingSystem.BuildingBlocks.csproj src/BuildingBlocks/
COPY src/Services/Payment/Payment.Domain/Payment.Domain.csproj src/Services/Payment/Payment.Domain/
COPY src/Services/Payment/Payment.Application/Payment.Application.csproj src/Services/Payment/Payment.Application/
COPY src/Services/Payment/Payment.Persistence/Payment.Persistence.csproj src/Services/Payment/Payment.Persistence/
COPY src/Services/Payment/Payment.Infrastructure/Payment.Infrastructure.csproj src/Services/Payment/Payment.Infrastructure/
COPY src/Services/Payment/Payment.WebAPI/Payment.WebAPI.csproj src/Services/Payment/Payment.WebAPI/
RUN dotnet restore src/Services/Payment/Payment.WebAPI/Payment.WebAPI.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Payment/ src/Services/Payment/
RUN dotnet publish src/Services/Payment/Payment.WebAPI/Payment.WebAPI.csproj \
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
ENTRYPOINT ["dotnet", "Payment.WebAPI.dll"]

