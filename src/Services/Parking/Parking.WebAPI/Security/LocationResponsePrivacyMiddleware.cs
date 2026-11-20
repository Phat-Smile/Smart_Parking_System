using Microsoft.AspNetCore.Http;

namespace ParkingSystem.Parking.WebAPI.Security;

/// <summary>
/// US-PRIV-001: prevent browsers and intermediary HTTP caches from retaining responses
/// computed with a customer's GPS location. Request URLs are already sanitized by
/// structured logging and tracing; this is the response-cache protection boundary.
/// </summary>
internal sealed class LocationResponsePrivacyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method) && IsLocationRoute(context.Request.Path))
        {
            // Set before calling the next middleware so 400/403/500 responses are protected too.
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
        }

        await next(context);
    }

    internal static bool IsLocationRoute(PathString path) =>
        path.StartsWithSegments("/api/parking/lots/search", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api/parking/lots/nearby", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api/parking/lots/recommendations", StringComparison.OrdinalIgnoreCase);
}
