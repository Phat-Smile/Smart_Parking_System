using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.Common.Booking;
using ParkingSystem.Parking.Application.Common.Mapbox;
using ParkingSystem.Parking.Application.Common.Realtime;
using ParkingSystem.Parking.Infrastructure.Booking;
using ParkingSystem.Parking.Infrastructure.Mapbox;
using ParkingSystem.Parking.Infrastructure;
using ParkingSystem.Parking.Infrastructure.Messaging.Consumers;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Parking.WebAPI.Endpoints;
using ParkingSystem.Parking.WebAPI.OpenApi;
using ParkingSystem.Parking.WebAPI.Grpc;
using ParkingSystem.Parking.WebAPI.Hubs;
using ParkingSystem.Parking.WebAPI.Realtime;
using ParkingSystem.Parking.WebAPI.Security;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.Recommendations;
using ParkingSystem.Parking.WebAPI.Clients;
using ParkingSystem.Parking.WebAPI.Geocoding;
using ParkingSystem.Parking.WebAPI.Routing;

var builder = WebApplication.CreateBuilder(args);
builder.AddParkingSystemWebFoundation();
builder.AddParkingSystemSecurity();
builder.AddParkingSystemDeviceKey();
builder.AddParkingSystemTelemetry("parking-api");
builder.Services.AddParkingAuthorization();
builder.Services.AddApplicationServices();
builder.Services.AddMemoryCache();
// OSRM Table is added for S4 search only; existing Mapbox adapter remains untouched.
builder.Services.AddSingleton<OsrmMatrixClient>(sp => new OsrmMatrixClient(
    new HttpClient { Timeout = TimeSpan.FromSeconds(2), MaxResponseContentBufferSize = 256 * 1024 },
    sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
    sp.GetRequiredService<IConfiguration>()));
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

// Mapbox client is opt-in: no token is required until a provider method is invoked.
// A singleton raw HttpClient avoids HttpClientFactory URL logging of the query-string token.
builder.Services.AddSingleton<IMapboxGeoRouting>(_ => new MapboxGeoRoutingClient(
    new HttpClient { Timeout = TimeSpan.FromSeconds(6), MaxResponseContentBufferSize = 1024 * 1024 },
    builder.Configuration["Mapbox:AccessToken"] ?? string.Empty));
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddIdempotentConsumer<ParkingSessionPaidConsumer>("parking.session-paid.v1");
builder.Services.AddInfrastructure(builder.Configuration, bus => bus.AddConsumer<ParkingSessionPaidConsumer>());
builder.Services.AddIdentityServiceClient(builder.Configuration);
builder.Services.Configure<BookingServiceOptions>(builder.Configuration.GetSection(BookingServiceOptions.SectionName));
builder.Services.AddHttpClient<IBookingCheckInClient, BookingCheckInHttpClient>();
builder.Services.AddHttpClient<ArrivalAvailabilityClient>(client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddHttpClient<IParkingRecommendationClient, ParkingRecommendationClient>(client => client.Timeout = TimeSpan.FromSeconds(2));
builder.Services.AddSignalR();
builder.Services.AddSingleton<IParkingRealtimeNotifier, SignalRParkingRealtimeNotifier>();
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
app.MapParkingLotRecommendationEndpoints();
app.MapArrivalAvailabilityEndpoints();
app.MapAdminParkingLotReviewEndpoints();
app.MapParkingLotAddressSearchEndpoints();
app.MapParkingLotOpeningHoursEndpoints();
app.MapParkingLotPublicInfoEndpoints();
app.MapZoneCapacityEndpoints();
app.MapParkingLotAssignmentEndpoints();
app.MapStaffLotEndpoints();
app.MapGateReadingEndpoints();
app.MapPricingEndpoints();
app.MapDepositPolicyEndpoints();
app.MapReservationRatioPolicyEndpoints();
app.MapReservationAvailabilityPolicyEndpoints();
app.MapParkingLayoutOwnerEndpoints();
app.MapParkingLayoutReadEndpoints();
app.MapSlotSensorEndpoints();
app.MapSlotConfirmationEndpoints();
app.MapHub<ParkingHub>("/hubs/parking");
app.MapGrpcService<ParkingAvailabilityGrpcService>();
app.MapGrpcService<ParkingSessionFeeGrpcService>();
app.MapGrpcService<ParkingLotMembershipGrpcService>();
app.MapGrpcHealthChecksService();
if (enableGrpcReflection)
    app.MapGrpcReflectionService();

var seedingEnabled = app.Configuration.GetValue<bool>("Seeding:Enabled");
if (seedingEnabled)
{
    if (app.Environment.IsDevelopment())
    {
        var reset = app.Configuration.GetValue<bool>("Seeding:Reset");
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingSystem.Parking.Persistence.ParkingDbContext>();
        var logger = scope.ServiceProvider.GetService<ILogger<ParkingSystem.Parking.Persistence.ParkingDbContext>>();
        await ParkingSystem.Parking.Persistence.Seeding.ParkingDataSeeder.SeedAsync(db, reset, logger);
    }
    else
    {
        var logger = app.Services.GetService<ILogger<ParkingSystem.Parking.Persistence.ParkingDbContext>>();
        logger?.LogWarning("Seeding is enabled in configuration but bypassed because environment is not Development.");
    }
}

app.Run();
