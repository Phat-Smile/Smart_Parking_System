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
            var errors = validationException.Errors
                .GroupBy(failure => string.IsNullOrWhiteSpace(failure.PropertyName) ? "_" : failure.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    _ => new[] { "The value is invalid." },
                    StringComparer.Ordinal);
            problem = new ValidationProblemDetails(errors)
            {
                Type = "about:blank",
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Detail = "Correct the invalid fields and retry.",
                Instance = context.Request.Path.Value
            };
            SafeProblemDetails.AddRequestExtensions(context, problem, "request.validation_failed");
        }
        else if (exception is BadHttpRequestException badHttpRequestException)
        {
            var statusCode = badHttpRequestException.StatusCode;
            problem = SafeProblemDetails.Create(context, statusCode);
            problem.Title = "Invalid request format.";
            problem.Detail = "Failed to parse request body. Please ensure the request body is valid JSON.";
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
            problem.Title = "An unexpected error occurred.";
            problem.Detail = "An unexpected error occurred. Please try again later.";
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
