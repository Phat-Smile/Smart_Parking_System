using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ParkingSystem.AspNetCore.Security;

public sealed class ServiceKeyValidationMiddleware(RequestDelegate next, IOptions<ServiceKeyOptions> options)
{
    public const string HeaderName = "X-Service-Key";

    public Task InvokeAsync(HttpContext context)
    {
        var requiredKey = options.Value.ServiceKey;
        if (string.IsNullOrEmpty(requiredKey) || IsHealthProbe(context.Request.Path))
            return next(context);

        if (!context.Request.Headers.TryGetValue(HeaderName, out var providedKey)
            || !KeysMatch(requiredKey, providedKey.ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        return next(context);
    }

    // Container and orchestrator probes carry no key and expose no business data.
    private static bool IsHealthProbe(PathString path) => path.StartsWithSegments("/health");

    private static bool KeysMatch(string required, string provided) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(required), Encoding.UTF8.GetBytes(provided));
}
