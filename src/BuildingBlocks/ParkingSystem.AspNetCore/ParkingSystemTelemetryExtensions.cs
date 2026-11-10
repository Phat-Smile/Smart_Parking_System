using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ParkingSystem.AspNetCore;

/// <summary>Registers host-only tracing and metrics. Export remains optional when no OTLP endpoint is configured.</summary>
public static class ParkingSystemTelemetryExtensions
{
    public static WebApplicationBuilder AddParkingSystemTelemetry(
        this WebApplicationBuilder builder,
        string defaultServiceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultServiceName);

        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"];
        if (string.IsNullOrWhiteSpace(serviceName))
            serviceName = defaultServiceName;

        var serviceVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        var hasOtlpEndpoint = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: serviceName,
                serviceNamespace: "ParkingSystem",
                serviceVersion: serviceVersion)
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName)]))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = context => ShouldTraceHttpRequest(context);
                        options.EnrichWithHttpRequest = SanitizeHttpRequestActivity;
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.EnrichWithHttpRequestMessage = SanitizeHttpClientActivity;
                    })
                    // MassTransit v8 emits its own ActivitySource. Do not create parallel send/receive spans.
                    .AddSource("MassTransit");

                if (hasOtlpEndpoint)
                    tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (hasOtlpEndpoint)
                    metrics.AddOtlpExporter();
            });

        return builder;
    }

    private static bool ShouldTraceHttpRequest(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return !path.Equals("/health/live", StringComparison.OrdinalIgnoreCase)
            && !path.Equals("/health/ready", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/grpc.health.v1.Health/", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/grpc.reflection.", StringComparison.OrdinalIgnoreCase);
    }

    private static void SanitizeHttpRequestActivity(Activity activity, HttpRequest request)
    {
        // Keep route templates/method/status. Never export raw paths or query strings, which can carry identifiers/secrets.
        activity.SetTag("url.full", null);
        activity.SetTag("url.path", null);
        activity.SetTag("url.query", null);
        activity.SetTag("http.url", null);
        activity.SetTag("http.target", null);

        var correlationContext = CorrelationIdMiddleware.GetCorrelationContext(request.HttpContext);
        activity.SetTag("correlation.id", correlationContext.CorrelationId.ToString("D"));
    }

    private static void SanitizeHttpClientActivity(Activity activity, HttpRequestMessage request)
    {
        // HttpClient URLs may contain opaque IDs or credentials in query parameters.
        activity.SetTag("url.full", null);
        activity.SetTag("url.path", null);
        activity.SetTag("url.query", null);
        activity.SetTag("http.url", null);
        activity.SetTag("http.target", null);

        if (request.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values))
        {
            var id = values.FirstOrDefault();
            if (!string.IsNullOrEmpty(id))
                activity.SetTag("correlation.id", id);
        }
    }
}
