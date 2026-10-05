using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace ParkingSystem.AspNetCore;

internal sealed class StructuredRequestLoggingMiddleware(RequestDelegate next, ILogger<StructuredRequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var method = context.Request.Method;
            var path = context.GetEndpoint() is RouteEndpoint endpoint
                ? endpoint.RoutePattern.RawText ?? "<matched>"
                : "<unmatched>";
            var statusCode = context.Response.StatusCode;
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            try
            {
                if (statusCode >= 500)
                {
                    logger.LogError("HTTP request completed. Method={Method} Path={Path} StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId} TraceId={TraceId} SpanId={SpanId} TraceIdentifier={TraceIdentifier}",
                        method, path, statusCode, elapsedMs, context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString(), CorrelationIdMiddleware.GetTraceId(context), CorrelationIdMiddleware.GetSpanId(), context.TraceIdentifier);
                }
                else if (statusCode >= 400)
                {
                    logger.LogInformation("HTTP request completed. Method={Method} Path={Path} StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId} TraceId={TraceId} SpanId={SpanId} TraceIdentifier={TraceIdentifier}",
                        method, path, statusCode, elapsedMs, context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString(), CorrelationIdMiddleware.GetTraceId(context), CorrelationIdMiddleware.GetSpanId(), context.TraceIdentifier);
                }
                else if (IsHealthCheck(path))
                {
                    logger.LogDebug("HTTP request completed. Method={Method} Path={Path} StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId} TraceId={TraceId} SpanId={SpanId} TraceIdentifier={TraceIdentifier}",
                        method, path, statusCode, elapsedMs, context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString(), CorrelationIdMiddleware.GetTraceId(context), CorrelationIdMiddleware.GetSpanId(), context.TraceIdentifier);
                }
                else
                {
                    logger.LogInformation("HTTP request completed. Method={Method} Path={Path} StatusCode={StatusCode} DurationMs={DurationMs} CorrelationId={CorrelationId} TraceId={TraceId} SpanId={SpanId} TraceIdentifier={TraceIdentifier}",
                        method, path, statusCode, elapsedMs, context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString(), CorrelationIdMiddleware.GetTraceId(context), CorrelationIdMiddleware.GetSpanId(), context.TraceIdentifier);
                }
            }
            catch
            {
                // A logging-provider failure must not change an HTTP response.
            }
        }
    }

    private static bool IsHealthCheck(string path) =>
        path.Equals("/health/live", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/health/ready", StringComparison.OrdinalIgnoreCase);
}
