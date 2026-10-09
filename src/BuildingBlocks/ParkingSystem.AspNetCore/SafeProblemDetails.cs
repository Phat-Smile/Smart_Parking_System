using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ParkingSystem.AspNetCore;

internal static class SafeProblemDetails
{
    public const string ContentType = "application/problem+json";
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ProblemDetails Create(HttpContext context, int statusCode)
    {
        var isServerError = statusCode >= 500;
        var isUnauthorized = statusCode == StatusCodes.Status401Unauthorized;
        var isForbidden = statusCode == StatusCodes.Status403Forbidden;
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Status = statusCode,
            Title = isUnauthorized
                ? "Chưa đăng nhập."
                : isForbidden
                    ? "Không có quyền truy cập."
                    : isServerError
                        ? "Đã xảy ra lỗi hệ thống."
                        : "Không thể xử lý yêu cầu.",
            Detail = isUnauthorized
                ? "Vui lòng đăng nhập để tiếp tục."
                : isForbidden
                    ? "Tài khoản của bạn không có quyền thực hiện thao tác này."
                    : isServerError
                        ? "Đã xảy ra sự cố trên máy chủ. Vui lòng thử lại sau."
                        : "Không thể xử lý yêu cầu. Vui lòng kiểm tra lại và thử lại.",
            Instance = context.Request.Path.Value
        };
        AddRequestExtensions(context, problem, isUnauthorized
            ? "auth.unauthorized"
            : isForbidden
                ? "auth.forbidden"
                : isServerError
                    ? "internal.unexpected"
                    : "http.request_failed");
        return problem;
    }

    public static void AddRequestExtensions(HttpContext context, ProblemDetails problem, string errorCode)
    {
        problem.Extensions["traceId"] = CorrelationIdMiddleware.GetTraceId(context);
        problem.Extensions["correlationId"] = CorrelationIdMiddleware.GetCorrelationContext(context).CorrelationId.ToString("D");
        problem.Extensions["errorCode"] = errorCode;
    }
}
