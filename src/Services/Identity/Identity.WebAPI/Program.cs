using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Identity.Application;
using ParkingSystem.Identity.Infrastructure;
using ParkingSystem.Identity.Infrastructure.VehicleLookup;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Identity.WebAPI.Endpoints;
using ParkingSystem.Identity.WebAPI.Grpc;
using ParkingSystem.Identity.WebAPI.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddParkingSystemWebFoundation();
builder.AddParkingSystemSecurity();
builder.Services.AddIdentityAuthorization();
builder.Services.AddMfa(builder.Configuration);
builder.AddParkingSystemTelemetry("identity-api");
builder.Services.AddApplicationServices();
builder.Services.AddPersistence(builder.Configuration);
VehicleLookupRegistration.EnsureProviderConfigured(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdentityDbContext>(name: "postgresql", tags: ["ready"]);
builder.Services.AddGrpcHealthChecks(options =>
        options.Services.Map("", check => check.Tags.Contains("grpc")))
    .AddCheck("grpc-server", () => HealthCheckResult.Healthy(), tags: ["grpc"]);
builder.Services.AddGrpc();
var enableGrpcReflection = builder.Environment.IsDevelopment();
if (enableGrpcReflection)
    builder.Services.AddGrpcReflection();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    var appXmlPath = Path.Combine(AppContext.BaseDirectory, "Identity.Application.xml");
    if (File.Exists(appXmlPath))
    {
        options.IncludeXmlComments(appXmlPath);
    }
});

var app = builder.Build();
if (!app.Environment.IsDevelopment() && VehicleLookupRegistration.IsStubConfigured(builder.Configuration))
    app.Logger.LogWarning("VehicleLookup uses the Stub provider outside Development: vehicle lookups return demo data.");
app.UseParkingSystemWebFoundation();
app.UseParkingSystemServiceSecurity();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
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
app.MapAccountEndpoints();
app.MapOwnerDocumentEndpoints();
app.MapCustomerMediaEndpoints();
app.MapAuditLogEndpoints();
app.MapGrpcService<IdentityVehicleGrpcService>();
app.MapGrpcHealthChecksService();
if (enableGrpcReflection)
    app.MapGrpcReflectionService();
app.Run();
