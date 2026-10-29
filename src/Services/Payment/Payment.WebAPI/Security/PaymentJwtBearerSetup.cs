using Microsoft.AspNetCore.Authentication.JwtBearer;
using ParkingSystem.Payment.WebAPI.Common;

namespace ParkingSystem.Payment.WebAPI.Security;

// Ép lỗi xác thực và phân quyền về định dạng chuẩn của nhóm
internal static class PaymentJwtBearerSetup
{
    public static IServiceCollection OverrideJwtBearerToApiResponse(
        this IServiceCollection services)
    {
        services.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new JwtBearerEvents();

            options.Events.OnChallenge = async context =>
            {
                // Chặn không cho hệ thống in lỗi mặc định
                context.HandleResponse();
                if (context.Response.HasStarted) return;

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.CacheControl = "no-store";
                await context.Response.WriteAsJsonAsync(
                    ApiResponse<object>.Failure(
                        "Authentication is required.",
                        StatusCodes.Status401Unauthorized));
            };

            options.Events.OnForbidden = async context =>
            {
                if (context.Response.HasStarted) return;

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.Headers.CacheControl = "no-store";
                await context.Response.WriteAsJsonAsync(
                    ApiResponse<object>.Failure(
                        "You do not have permission to access this resource.",
                        StatusCodes.Status403Forbidden));
            };
        });

        return services;
    }
}
