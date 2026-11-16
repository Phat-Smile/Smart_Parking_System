using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Payment.Application;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Infrastructure;
using ParkingSystem.Payment.Persistence;
using ParkingSystem.Payment.WebAPI.Endpoints;
using ParkingSystem.Payment.WebAPI.Grpc;
using ParkingSystem.Payment.WebAPI.Security;
using ParkingSystem.Payment.Application.Common.Options;

var builder = WebApplication.CreateBuilder(args);
builder.AddParkingSystemWebFoundation();
builder.AddParkingSystemSecurity();
builder.AddParkingSystemTelemetry("payment-api");
builder.Services.AddPaymentAuthorization();
builder.Services.AddPaymentRateLimiting(builder.Configuration);
builder.Services.AddScoped<ICurrentActor, CurrentActor>();
builder.Services.Configure<WalletOptions>(builder.Configuration.GetSection(WalletOptions.SectionName));
builder.Services.Configure<VnpayOptions>(builder.Configuration.GetSection(VnpayOptions.SectionName));
builder.Services.AddApplicationServices();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddParkingSessionFeeClient(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PaymentDbContext>(name: "postgresql", tags: ["ready"]);
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
app.UseParkingSystemServiceSecurity();
app.UseRateLimiter();
app.MapVnpayEndpoints();
app.MapWalletEndpoints();
app.MapParkingFeeEndpoints();
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
app.MapGrpcService<ReservationWalletGrpcService>();
if (enableGrpcReflection)
    app.MapGrpcReflectionService();
app.MapAuditLogEndpoints();
app.Run();
