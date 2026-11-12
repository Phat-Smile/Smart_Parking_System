using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Notification.Application;
using ParkingSystem.Notification.Infrastructure;
using ParkingSystem.Notification.Persistence;
using ParkingSystem.Notification.WebAPI;

// In local development, automatically load environment variables from the root .env file if available
try
{
    var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
    DirectoryInfo? dir = currentDir;
    while (dir != null)
    {
        var envPath = Path.Combine(dir.FullName, ".env");
        if (File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;
                var splitIndex = trimmed.IndexOf('=');
                if (splitIndex <= 0) continue;
                var key = trimmed[..splitIndex].Trim();
                var val = trimmed[(splitIndex + 1)..].Trim();
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                {
                    Environment.SetEnvironmentVariable(key, val);
                }
            }
            break;
        }
        dir = dir.Parent;
    }
}
catch
{
    // Ignore any .env file reading errors
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.AddParkingSystemWebFoundation();
builder.AddParkingSystemSecurity();
builder.AddParkingSystemTelemetry("notification-api");
builder.Services.AddApplicationServices();
builder.Services.AddPersistence(builder.Configuration);
if (!builder.Environment.IsDevelopment() && ParkingSystem.Notification.Infrastructure.DependencyInjection.UsesConsoleEmailSender(builder.Configuration))
{
    // The console sender logs OTP codes; it is a local-development convenience only.
    throw new InvalidOperationException("SMTP_HOST is required outside Development: the console email sender would write OTP codes to the logs.");
}

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<NotificationDbContext>(name: "postgresql", tags: ["ready"]);
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
app.MapCustomerNotificationEndpoints();
if (enableGrpcReflection)
    app.MapGrpcReflectionService();
app.Run();
