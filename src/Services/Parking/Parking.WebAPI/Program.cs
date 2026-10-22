using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Infrastructure;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Parking.WebAPI.Endpoints;
using ParkingSystem.Parking.WebAPI.OpenApi;
using ParkingSystem.Parking.WebAPI.Grpc;
using ParkingSystem.Parking.WebAPI.Security;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.WebAPI.Geocoding;

var builder = WebApplication.CreateBuilder(args);
builder.AddParkingSystemWebFoundation();
builder.AddParkingSystemSecurity();
builder.AddParkingSystemTelemetry("parking-api");
builder.Services.AddParkingAuthorization();
builder.Services.AddApplicationServices();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<NominatimRequestGate>();
builder.Services.AddHttpClient<IAddressGeocoder, NominatimAddressGeocoder>(client =>
{
    var endpoint = builder.Configuration["Nominatim:BaseUrl"]
        ?? "https://nominatim.openstreetmap.org/";
    client.BaseAddress = new Uri(
        endpoint.EndsWith('/') ? endpoint : endpoint + "/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        builder.Configuration["Nominatim:UserAgent"]
        ?? "ParkWise-OJT/1.0 (+https://github.com/Phat-Smile/Smart_Parking_System)");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ParkingDbContext>(name: "postgresql", tags: ["ready"]);
builder.Services.AddGrpcHealthChecks(options =>
        options.Services.Map("", check => check.Tags.Contains("grpc")))
    .AddCheck("grpc-server", () => HealthCheckResult.Healthy(), tags: ["grpc"]);
builder.Services.AddGrpc();
var enableGrpcReflection = builder.Environment.IsDevelopment();
if (enableGrpcReflection)
    builder.Services.AddGrpcReflection();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddParkingSwagger();

var app = builder.Build();
app.UseParkingSystemWebFoundation();
app.UseParkingSystemServiceSecurity();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResultStatusCodes =
    {
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});
app.MapParkingSessionEndpoints();
app.MapParkingLotEndpoints();
app.MapParkingLotAddressSearchEndpoints();
app.MapParkingLotAssignmentEndpoints();
app.MapGrpcService<ParkingAvailabilityGrpcService>();
app.MapGrpcHealthChecksService();
if (enableGrpcReflection)
    app.MapGrpcReflectionService();
app.Run();
