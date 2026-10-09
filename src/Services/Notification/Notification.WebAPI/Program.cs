using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Notification.Application;
using ParkingSystem.Notification.Infrastructure;
using ParkingSystem.Notification.Persistence;
LoadRootEnvFile();

var builder = WebApplication.CreateBuilder(args);

static void LoadRootEnvFile()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
        var envPath = Path.Combine(current.FullName, ".env");
        if (File.Exists(envPath))
        {
            var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;
                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex > 0)
                {
                    var key = trimmed[..separatorIndex].Trim();
                    var val = trimmed[(separatorIndex + 1)..].Trim();
                    env[key] = val;
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    {
                        Environment.SetEnvironmentVariable(key, val);
                    }
                }
            }

            var isContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
            var rabbitHost = isContainer
                ? env.GetValueOrDefault("RABBITMQ_HOST", "rabbitmq")
                : "127.0.0.1";
            var rabbitPort = isContainer
                ? env.GetValueOrDefault("RABBITMQ_PORT", "5672")
                : env.GetValueOrDefault("RABBITMQ_HOST_PORT", "5672");
            var rabbitUser = env.GetValueOrDefault("RABBITMQ_USER", "parking_dev");
            var rabbitPass = env.GetValueOrDefault("RABBITMQ_PASSWORD", "parking_dev_local_only");
            var rabbitVHost = env.GetValueOrDefault("RABBITMQ_VHOST", "/");

            Environment.SetEnvironmentVariable("RabbitMq__Host", rabbitHost);
            Environment.SetEnvironmentVariable("RabbitMq__Port", rabbitPort);
            Environment.SetEnvironmentVariable("RabbitMq__Username", rabbitUser);
            Environment.SetEnvironmentVariable("RabbitMq__Password", rabbitPass);
            Environment.SetEnvironmentVariable("RabbitMq__VirtualHost", rabbitVHost);

            var pgHost = isContainer
                ? env.GetValueOrDefault("POSTGRES_HOST", "postgres")
                : "127.0.0.1";
            var pgPort = isContainer
                ? env.GetValueOrDefault("POSTGRES_PORT", "5432")
                : env.GetValueOrDefault("POSTGRES_HOST_PORT", "5433");
            var pgDb = env.GetValueOrDefault("POSTGRES_DB", "parking_system");
            var pgUser = env.GetValueOrDefault("POSTGRES_USER", "parking_dev");
            var pgPass = env.GetValueOrDefault("POSTGRES_PASSWORD", "parking_dev_local_only");

            var connStr = $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPass};GSS Encryption Mode=Disable";
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connStr);
            break;
        }
        current = current.Parent;
    }
}
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
