using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ParkingSystem.BuildingBlocks.Identity;

namespace ParkingSystem.AspNetCore.Security;

public static class ParkingSystemSecurityExtensions
{
    public const string AuthenticatedUserPolicy = "authenticated-user";
    public const string GatewayCorsPolicy = "parking-gateway";

    public static WebApplicationBuilder AddParkingSystemSecurity(this WebApplicationBuilder builder)
    {
        // The framework's token-failure logs can include IdentityModel diagnostic identifiers and details.
        // Authentication responses already use a safe challenge; suppress these diagnostic categories.
        builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication.JwtBearer", LogLevel.None);
        builder.Logging.AddFilter("Microsoft.IdentityModel", LogLevel.None);
        // SignalR passes tokens in ?access_token=; prevent Hosting.Diagnostics from logging full request URLs
        // at Information level on non-console providers (StructuredRequestLoggingMiddleware logs sanitized routes).
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
        builder.AddParkingSystemServiceKeyValidation();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
        builder.Services.AddSingleton<IValidateOptions<JwtSecurityOptions>, JwtSecurityOptionsValidator>();
        builder.Services.AddOptions<JwtSecurityOptions>()
            .Bind(builder.Configuration.GetSection(JwtSecurityOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<JwtValidationKey>();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSecurityOptions>, JwtValidationKey>((options, securityOptions, validationKey) =>
            {
                var settings = securityOptions.Value;
                options.MapInboundClaims = false;
                options.SaveToken = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = validationKey.SecurityKey,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ClockSkew = TimeSpan.FromSeconds(settings.ClockSkewSeconds)
                };

                options.Events ??= new JwtBearerEvents();
                var previousOnMessageReceived = options.Events.OnMessageReceived;
                options.Events.OnMessageReceived = async context =>
                {
                    if (previousOnMessageReceived is not null)
                    {
                        await previousOnMessageReceived(context);
                    }

                    if (!string.IsNullOrEmpty(context.Token))
                    {
                        return;
                    }

                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path.Value;
                    if (!string.IsNullOrWhiteSpace(accessToken) &&
                        path is not null &&
                        path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Token = accessToken;
                    }
                };
            });

        builder.Services.AddAuthorization(options =>
            options.AddPolicy(AuthenticatedUserPolicy, policy => policy.RequireAuthenticatedUser()));

        return builder;
    }

    public static WebApplicationBuilder AddParkingSystemGatewaySecurity(this WebApplicationBuilder builder)
    {
        builder.AddParkingSystemSecurity();

        builder.Services.AddOptions<GatewayCorsOptions>()
            .Bind(builder.Configuration.GetSection(GatewayCorsOptions.SectionName))
            .Validate(options => GatewayCorsOptions.ParseAllowedOrigins(options.AllowedOrigins)
                .All(GatewayCorsOptions.IsValidOrigin), "Security:Cors:AllowedOrigins must contain explicit HTTP or HTTPS origins only.")
            .ValidateOnStart();
        var origins = GatewayCorsOptions.ParseAllowedOrigins(
            builder.Configuration[$"{GatewayCorsOptions.SectionName}:AllowedOrigins"]);
        builder.Services.AddCors(options => options.AddPolicy(GatewayCorsPolicy, policy =>
        {
            if (origins.Length > 0)
                policy.WithOrigins(origins);

            policy.WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                .WithHeaders("Authorization", "Content-Type", "X-Correlation-ID", "Idempotency-Key")
                .WithExposedHeaders("X-Correlation-ID");
        }));

        return builder;
    }

    public static WebApplicationBuilder AddParkingSystemServiceKeyValidation(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<ServiceKeyOptions>()
            .Bind(builder.Configuration.GetSection(ServiceKeyOptions.SectionName));
        return builder;
    }

    public static WebApplication UseParkingSystemAuthentication(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    // Pipeline for the five business services: the key proves the call came through the gateway, then the JWT proves the user.
    public static WebApplication UseParkingSystemServiceSecurity(this WebApplication app)
    {
        app.UseServiceKeyValidation();
        app.UseParkingSystemAuthentication();
        return app;
    }

    public static WebApplication UseServiceKeyValidation(this WebApplication app)
    {
        app.UseMiddleware<ServiceKeyValidationMiddleware>();
        return app;
    }

    public static WebApplication UseParkingSystemGatewaySecurity(this WebApplication app)
    {
        app.UseMiddleware<GatewaySecurityHeadersMiddleware>();
        app.UseCors(GatewayCorsPolicy);
        app.UseParkingSystemAuthentication();
        return app;
    }

    public static RouteGroupBuilder MapAuthenticatedGroup(this IEndpointRouteBuilder endpoints, string prefix) =>
        endpoints.MapGroup(prefix).RequireAuthorization(AuthenticatedUserPolicy);

    public static TBuilder RequireAuthenticatedUser<TBuilder>(this TBuilder endpointBuilder)
        where TBuilder : IEndpointConventionBuilder =>
        endpointBuilder.RequireAuthorization(AuthenticatedUserPolicy);
}
