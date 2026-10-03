using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Identity.Application;
using ParkingSystem.Identity.Infrastructure;
using ParkingSystem.Identity.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.AddParkingSystemWebFoundation();
builder.AddParkingSystemSecurity();
builder.AddParkingSystemTelemetry("identity-api");
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, listen => listen.Protocols = HttpProtocols.Http1);
    options.ListenAnyIP(8081, listen => listen.Protocols = HttpProtocols.Http2);
});
builder.Services.AddApplicationServices();
builder.Services.AddPersistence(builder.Configuration);
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseParkingSystemWebFoundation();
app.UseParkingSystemAuthentication();
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
app.MapGrpcHealthChecksService();
if (enableGrpcReflection)
    app.MapGrpcReflectionService();
app.Run();
