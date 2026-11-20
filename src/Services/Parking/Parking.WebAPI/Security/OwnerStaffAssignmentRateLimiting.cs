using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ParkingSystem.Parking.WebAPI.Security;

public static class OwnerStaffAssignmentRateLimiting
{
    public const string PolicyName = "OwnerStaffAssignmentRateLimitPolicy";
    public const int DefaultPermitsPerMinute = 10;
    public const string ErrorCode = "parking.rate_limited";

    public static IServiceCollection AddOwnerStaffAssignmentRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PolicyName, context =>
            {
                var ownerId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? context.User.FindFirst("sub")?.Value;

                if (string.IsNullOrWhiteSpace(ownerId))
                {
                    ownerId = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();
                    if (string.IsNullOrWhiteSpace(ownerId))
                        ownerId = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    ownerId,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = DefaultPermitsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                var http = context.HttpContext;
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                    : 60;
                http.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

                var problem = new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc6585#section-4",
                    Title = "Too Many Requests",
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = "Bạn đã thực hiện quá nhiều yêu cầu phân công nhân viên. Vui lòng thử lại sau.",
                    Instance = http.Request.Path
                };
                problem.Extensions["errorCode"] = ErrorCode;
                problem.Extensions["retryAfter"] = retryAfterSeconds;

                http.Response.ContentType = "application/problem+json";
                await http.Response.WriteAsJsonAsync(problem, cancellationToken);
            };
        });

        return services;
    }
}
