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
                ? "Unauthorized"
                : isForbidden
                    ? "Forbidden"
                    : isServerError
                        ? "An unexpected error occurred."
                        : "The request could not be completed.",
            Detail = isUnauthorized
                ? "Authentication is required to access this resource."
                : isForbidden
                    ? "The authenticated user is not allowed to access this resource."
                    : isServerError
                        ? "An unexpected error occurred. Please try again later."
                        : "The request could not be processed.",
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
