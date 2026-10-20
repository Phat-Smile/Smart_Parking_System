using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.AspNetCore;

// Business errors returned by endpoints (404, 409...) use the same problem+json shape as the
// foundation errors: instance, traceId, correlationId and errorCode (tester report 2026-10-08, DOC-003).
public static class BusinessProblem
{
    public static IResult Create(HttpContext context, int statusCode, Error error)
    {
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Status = statusCode,
            Title = error.Message,
            Instance = context.Request.Path.Value
        };
        SafeProblemDetails.AddRequestExtensions(context, problem, error.Code);
        return Results.Problem(problem);
    }
}
