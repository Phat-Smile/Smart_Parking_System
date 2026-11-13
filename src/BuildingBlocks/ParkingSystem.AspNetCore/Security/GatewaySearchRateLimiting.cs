using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ParkingSystem.AspNetCore.Security;

// US-AUTH-007: Giới hạn số request tìm kiếm bãi xe theo IP (tối đa 60 req/phút/IP), chặn spam và bảo vệ hạ tầng bản đồ / gợi ý bãi xe.
public static class GatewaySearchRateLimiting
{
    public const string SectionName = "RateLimiting:Search";
    public const string PolicyName = "parking-search";
    public const int DefaultPermitsPerMinute = 60;
    public const string RejectedErrorCode = "gateway.search_rate_limited";

    public static WebApplicationBuilder AddParkingSystemSearchRateLimiting(this WebApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(options =>
        {
            var permits = builder.Configuration.GetValue($"{SectionName}:{PolicyName}", DefaultPermitsPerMinute);
            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permits,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            options.OnRejected = WriteRejectionAsync;
        });

        return builder;
    }

    // The Gateway registers the auth and the search limiter, and the one registered last owns OnRejected.
    // It therefore answers for both: only the search route gets the search wording.
    private static ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var policy = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        return policy == PolicyName
            ? WriteSearchRejectionAsync(context, cancellationToken)
            : GatewayAuthRateLimiting.WriteRejectionAsync(context, cancellationToken);
    }

    private static async ValueTask WriteSearchRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : 60;
        http.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        var problem = SafeProblemDetails.Create(http, StatusCodes.Status429TooManyRequests);
        problem.Title = "Bạn thao tác tìm kiếm quá nhanh.";
        problem.Detail = $"Bạn đã gửi quá nhiều yêu cầu tìm kiếm bãi xe. Vui lòng thử lại sau {retryAfterSeconds} giây.";
        problem.Extensions["errorCode"] = RejectedErrorCode;
        problem.Extensions["retryAfterSeconds"] = retryAfterSeconds;

        await http.Response.WriteAsJsonAsync(problem, SafeProblemDetails.JsonOptions, SafeProblemDetails.ContentType, cancellationToken);
    }
}
