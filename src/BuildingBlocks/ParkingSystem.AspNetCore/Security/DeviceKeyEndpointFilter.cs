using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ParkingSystem.AspNetCore.Security;

// Device routes are anonymous for users, so the device itself must prove who it is.
// Unlike the service key, an unset key rejects every call (fail closed).
public sealed class DeviceKeyEndpointFilter(IOptions<DeviceKeyOptions> options) : IEndpointFilter
{
    public const string HeaderName = "X-Device-Key";

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var requiredKey = options.Value.SensorKey;
        if (string.IsNullOrEmpty(requiredKey)
            || !context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var providedKey)
            || !KeysMatch(requiredKey, providedKey.ToString()))
        {
            return ValueTask.FromResult<object?>(Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Device key required.",
                extensions: new Dictionary<string, object?>
                {
                    ["errorCode"] = "device.key_invalid",
                    ["traceId"] = context.HttpContext.TraceIdentifier
                }));
        }

        return next(context);
    }

    private static bool KeysMatch(string required, string provided) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(required), Encoding.UTF8.GetBytes(provided));
}

public static class DeviceKeyExtensions
{
    public static WebApplicationBuilder AddParkingSystemDeviceKey(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<DeviceKeyOptions>()
            .Bind(builder.Configuration.GetSection(DeviceKeyOptions.SectionName));
        return builder;
    }

    public static RouteHandlerBuilder RequireDeviceKey(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<DeviceKeyEndpointFilter>();
}
