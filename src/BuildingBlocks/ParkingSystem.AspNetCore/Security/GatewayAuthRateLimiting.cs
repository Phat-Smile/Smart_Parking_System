using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ParkingSystem.AspNetCore.Security;

// TECH-10: giới hạn số request theo IP cho các route Auth công khai, chặn dò mật khẩu/OTP và spam email.
public static class GatewayAuthRateLimiting
{
    public const string SectionName = "RateLimiting:Auth";
    public const string RejectedErrorCode = "gateway.rate_limited";

    // Số request mỗi phút cho một IP (chốt D8b 08/10, thêm forgot/reset/google 09/10); ghi đè được qua config.
    public static readonly IReadOnlyDictionary<string, int> DefaultPermitsPerMinute = new Dictionary<string, int>
    {
        ["auth-login"] = 10,
        ["auth-register"] = 5,
        ["auth-resend-otp"] = 5,
        ["auth-verify-email"] = 10,
        ["auth-refresh"] = 30,
        ["auth-forgot-password"] = 5,
        ["auth-reset-password"] = 10,
        ["auth-google"] = 10
    };

    public static WebApplicationBuilder AddParkingSystemAuthRateLimiting(this WebApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(options =>
        {
            foreach (var (policy, defaultPermits) in DefaultPermitsPerMinute)
            {
                var permits = builder.Configuration.GetValue($"{SectionName}:{policy}", defaultPermits);
                // Chỉ dùng IP kết nối thật: chưa có proxy tin cậy phía trước nên X-Forwarded-For giả mạo được.
                options.AddPolicy(policy, context => RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permits,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
            }

            options.OnRejected = WriteRejectionAsync;
        });

        return builder;
    }

    // 429 cùng dạng problem+json với các lỗi khác, kèm Retry-After để FE biết chờ bao lâu.
    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : 60;
        http.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        var problem = SafeProblemDetails.Create(http, StatusCodes.Status429TooManyRequests);
        problem.Title = "Bạn thao tác quá nhanh.";
        problem.Detail = $"Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau {retryAfterSeconds} giây.";
        problem.Extensions["errorCode"] = RejectedErrorCode;
        problem.Extensions["retryAfterSeconds"] = retryAfterSeconds;

        await http.Response.WriteAsJsonAsync(problem, SafeProblemDetails.JsonOptions, SafeProblemDetails.ContentType, cancellationToken);
    }
}
