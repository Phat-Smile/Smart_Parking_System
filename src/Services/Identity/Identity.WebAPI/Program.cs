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

            var otpPepper = env.GetValueOrDefault("SECURITY_OTP_PEPPER", env.GetValueOrDefault("OTP_PEPPER", "dev_local_only_otp_pepper_secret_2026_smart_parking_secure_hmac"));
            Environment.SetEnvironmentVariable("Security__Otp__Pepper", otpPepper);

            break;
        }
        current = current.Parent;
    }
}
builder.AddParkingSystemWebFoundation();
builder.AddParkingSystemSecurity();
builder.Services.AddIdentityAuthorization();
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
app.MapAccountEndpoints();
app.MapGrpcService<IdentityVehicleGrpcService>();
app.MapGrpcHealthChecksService();
if (enableGrpcReflection)
    app.MapGrpcReflectionService();
app.Run();
