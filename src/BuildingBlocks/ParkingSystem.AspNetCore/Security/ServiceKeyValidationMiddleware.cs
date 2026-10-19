using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ParkingSystem.AspNetCore.Security;

public sealed class ServiceKeyValidationMiddleware(RequestDelegate next, IOptions<ServiceKeyOptions> options)
{
    public const string HeaderName = "X-Service-Key";

    public Task InvokeAsync(HttpContext context)
    {
        var requiredKey = options.Value.ServiceKey;
        if (string.IsNullOrEmpty(requiredKey))
            return next(context);

        if (!context.Request.Headers.TryGetValue(HeaderName, out var providedKey)
            || !string.Equals(providedKey, requiredKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        return next(context);
    }
}
