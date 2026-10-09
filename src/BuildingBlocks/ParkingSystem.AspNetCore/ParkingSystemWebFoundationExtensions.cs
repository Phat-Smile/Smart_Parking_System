using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using ParkingSystem.BuildingBlocks.Correlation;

namespace ParkingSystem.AspNetCore;

public static class ParkingSystemWebFoundationExtensions
{
    public const string ConsoleFormatterNameKey = "Logging:Console:FormatterName";

    public static WebApplicationBuilder AddParkingSystemWebFoundation(this WebApplicationBuilder builder)
    {
        DevelopmentEnvFile.AddTo(builder);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped(provider =>
        {
            var httpContext = provider.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>().HttpContext;
            return httpContext is null
                ? new CorrelationContext(Guid.Empty)
                : CorrelationIdMiddleware.GetCorrelationContext(httpContext);
        });
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<SafeExceptionHandler>();

        builder.Logging.ClearProviders();
        // ASP.NET Core's ambient scope includes the raw RequestPath. The request
        // event writes correlation and trace fields explicitly without that scope.
        // JSON stays the default (containers, tests); launch profiles opt into the
        // readable "simple" formatter for local F5 / dotnet run.
        if (string.Equals(builder.Configuration[ConsoleFormatterNameKey], ConsoleFormatterNames.Simple, StringComparison.OrdinalIgnoreCase))
        {
            builder.Logging.AddSimpleConsole(options =>
            {
                options.IncludeScopes = false;
                options.TimestampFormat = "HH:mm:ss ";
            });
        }
        else
        {
            builder.Logging.AddJsonConsole(options => options.IncludeScopes = false);
        }
        builder.Logging.AddFilter<ConsoleLoggerProvider>("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
        builder.Logging.AddFilter<ConsoleLoggerProvider>("Microsoft.AspNetCore.Routing.EndpointMiddleware", LogLevel.Warning);
        // The Outbox dispatcher polls every second; executed-SQL logs at Information drown out real errors.
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

        return builder;
    }

    public static WebApplication UseParkingSystemWebFoundation(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<StructuredRequestLoggingMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages(async (StatusCodeContext statusContext) =>
        {
            var context = statusContext.HttpContext;
            var problem = SafeProblemDetails.Create(context, context.Response.StatusCode);
            context.Response.ContentType = SafeProblemDetails.ContentType;
            await JsonSerializer.SerializeAsync(context.Response.Body, problem, SafeProblemDetails.JsonOptions, context.RequestAborted);
        });

        return app;
    }
}
