using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ParkingSystem.AspNetCore;

internal sealed class SafeExceptionHandler(
    ILogger<SafeExceptionHandler> logger,
    IConfiguration configuration) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (context.Response.HasStarted)
        {
            TryLog(() => logger.LogWarning(
                "An exception occurred after the response had started. ExceptionType={ExceptionType}",
                exception.GetType().FullName));
            return true;
        }

        ProblemDetails problem;
        if (exception is ValidationException validationException)
        {
            // One message per field: validators stop at the first failed rule, so the user sees the reason to fix first.
            var errors = validationException.Errors
                .GroupBy(failure => string.IsNullOrWhiteSpace(failure.PropertyName) ? "_" : failure.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => new[] { SafeMessage(group.First()) },
                    StringComparer.Ordinal);

            problem = new ValidationProblemDetails(errors)
            {
                Type = "about:blank",
                Status = StatusCodes.Status400BadRequest,
                Title = "Dữ liệu không hợp lệ.",
                Detail = string.Join(" ", errors.Values.Select(messages => messages[0])),
                Instance = context.Request.Path.Value
            };
            SafeProblemDetails.AddRequestExtensions(context, problem, "request.validation_failed");
        }
        else if (exception is BadHttpRequestException badHttpRequestException)
        {
            var statusCode = badHttpRequestException.StatusCode;
            problem = SafeProblemDetails.Create(context, statusCode);
            problem.Title = "Dữ liệu yêu cầu không hợp lệ.";
            problem.Detail = "Không thể phân tích nội dung yêu cầu. Vui lòng đảm bảo dữ liệu gửi lên là định dạng JSON hợp lệ.";
            SafeProblemDetails.AddRequestExtensions(context, problem, "request.malformed");
        }
        else
        {
            var exceptionDetails = SafeExceptionDiagnostics.Format(exception, configuration);
            TryLog(() => logger.LogError(
                "Unhandled request exception. ExceptionType={ExceptionType} ExceptionDetails={ExceptionDetails}",
                exception.GetType().FullName,
                exceptionDetails));
            problem = SafeProblemDetails.Create(context, StatusCodes.Status500InternalServerError);
        }

        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = SafeProblemDetails.ContentType;
        if (problem is ValidationProblemDetails validationProblem)
        {
            await System.Text.Json.JsonSerializer.SerializeAsync(
                context.Response.Body,
                validationProblem,
                SafeProblemDetails.JsonOptions,
                cancellationToken);
        }
        else
        {
            await System.Text.Json.JsonSerializer.SerializeAsync(
                context.Response.Body,
                problem,
                SafeProblemDetails.JsonOptions,
                cancellationToken);
        }
        return true;
    }

    // A message that echoes the submitted value (a password, a token) is never sent back. Values shorter than 4
    // characters are skipped: "a" or "6" would match ordinary words of the message and hide a useful reason.
    private const int MinEchoLength = 4;

    private static string SafeMessage(FluentValidation.Results.ValidationFailure failure)
    {
        var attempted = failure.AttemptedValue?.ToString();
        if (string.IsNullOrWhiteSpace(failure.ErrorMessage) ||
            (attempted is { Length: >= MinEchoLength } && failure.ErrorMessage.Contains(attempted, StringComparison.OrdinalIgnoreCase)))
            return "Giá trị không hợp lệ.";

        return failure.ErrorMessage;
    }

    private static void TryLog(Action writeLog)
    {
        try
        {
            writeLog();
        }
        catch
        {
            // Logging must not prevent the safe HTTP response from being written.
        }
    }
}
