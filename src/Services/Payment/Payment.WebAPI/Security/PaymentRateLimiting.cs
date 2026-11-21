using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ParkingSystem.Payment.WebAPI.Security;

// Giới hạn tần suất truy cập các route công khai ẩn danh của Payment service (US-AUTH-007 & PR-C Security Hardening).
// Ngăn chặn brute-force/DoS vào các endpoint tra cứu trạng thái và xác thực chữ ký VNPay trả về.
public static class PaymentRateLimiting
{
    public const string PolicyName = "AnonymousPublicStatusPolicy";
    public const string SectionName = "RateLimiting:PublicStatus";
    public const int DefaultPermitsPerMinute = 30;
    public const string PublicFeePolicyName = "AnonymousPublicFeePolicy";
    public const string PublicFeeSectionName = "RateLimiting:PublicFee";
    public const int DefaultPublicFeePermitsPerMinute = 20;
    public const string RejectedErrorCode = "payment.rate_limited";

    public static IServiceCollection AddPaymentRateLimiting(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        int? overridePermits = null)
    {
        services.AddRateLimiter(options =>
        {
            var permits = overridePermits
                ?? configuration?.GetValue<int?>($"{SectionName}:PermitsPerMinute")
                ?? DefaultPermitsPerMinute;

            options.AddPolicy(PolicyName, context =>
            {
                var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    clientIp,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permits,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });

            var publicFeePermits = overridePermits
                ?? configuration?.GetValue<int?>($"{PublicFeeSectionName}:PermitsPerMinute")
                ?? DefaultPublicFeePermitsPerMinute;

            options.AddPolicy(PublicFeePolicyName, context =>
            {
                var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    clientIp,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = publicFeePermits,
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
                    Type = "about:blank",
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Bạn thao tác quá nhanh.",
                    Detail = $"Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau {retryAfterSeconds} giây.",
                    Instance = http.Request.Path.Value
                };
                problem.Extensions["errorCode"] = RejectedErrorCode;
                problem.Extensions["retryAfterSeconds"] = retryAfterSeconds;

                http.Response.ContentType = "application/problem+json";
                await http.Response.WriteAsJsonAsync(problem, cancellationToken: cancellationToken);
            };
        });

        return services;
    }
}
