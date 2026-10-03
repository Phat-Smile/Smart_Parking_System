using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ParkingSystem.BuildingBlocks.Correlation;

namespace ParkingSystem.AspNetCore;

internal sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    private static readonly object ContextKey = typeof(CorrelationContext);

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers[HeaderName]);
        var correlationContext = new CorrelationContext(Guid.Parse(correlationId));
        context.Items[ContextKey] = correlationContext;
        context.Request.Headers[HeaderName] = correlationId;
        context.Response.OnStarting(static state =>
        {
            var (httpContext, value) = ((HttpContext, string))state;
            httpContext.Response.Headers[HeaderName] = value;
            return Task.CompletedTask;
        }, (context, correlationId));

        IDisposable? loggingScope = null;
        try
        {
            loggingScope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["CorrelationId"] = correlationId,
                ["TraceId"] = GetTraceId(context),
                ["SpanId"] = GetSpanId(),
                ["TraceIdentifier"] = context.TraceIdentifier
            });
        }
        catch
        {
            // A logging-provider failure must not prevent request handling.
        }

        try
        {
            await next(context);
        }
        finally
        {
            try
            {
                loggingScope?.Dispose();
            }
            catch
            {
                // A logging-provider failure must not replace a request result.
            }
        }
    }

    internal static string ResolveCorrelationId(Microsoft.Extensions.Primitives.StringValues values)
    {
        if (values.Count == 1)
        {
            var value = values[0];
            if (value is { Length: > 0 and <= 128 } &&
                string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
                Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
            {
                return value;
            }
        }

        return Guid.NewGuid().ToString("D");
    }

    internal static CorrelationContext GetCorrelationContext(HttpContext context)
    {
        if (context.Items.TryGetValue(ContextKey, out var value) && value is CorrelationContext correlationContext)
            return correlationContext;

        var correlationId = ResolveCorrelationId(context.Request.Headers[HeaderName]);
        var created = new CorrelationContext(Guid.Parse(correlationId));
        context.Items[ContextKey] = created;
        context.Response.Headers[HeaderName] = correlationId;
        return created;
    }

    internal static string GetTraceId(HttpContext context) =>
        Activity.Current?.TraceId.ToString() ?? string.Empty;

    internal static string GetSpanId() =>
        Activity.Current?.SpanId.ToString() ?? string.Empty;
}
