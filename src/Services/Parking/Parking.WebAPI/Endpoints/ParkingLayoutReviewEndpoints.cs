using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class ParkingLayoutReviewEndpoints
{
    public sealed record RejectLayoutRequest(string? Reason);

    public static IEndpointRouteBuilder MapParkingLayoutReviewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/parking/admin/layout-change-requests/pending", async (
            ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListPendingLayoutReviewsQuery(), ct)))
            .RequireAuthorization(ParkingAuthorization.SystemAdminPolicy)
            .WithName("ListPendingLayoutChanges")
            .Produces<IReadOnlyList<LayoutReviewRequest>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        const string reviewRoute = "/api/parking/admin/lots/{lotId:guid}/layout-change-requests/{requestId:guid}";
        app.MapPost(reviewRoute + "/approve", async (Guid lotId, Guid requestId,
            ICurrentUser currentUser, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            if (!Guid.TryParse(currentUser.SubjectId, out var adminId)) return Results.Forbid();
            var result = await sender.Send(new ReviewLayoutChangeCommand(lotId, requestId, adminId, true, null), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error!, http);
        })
        .RequireAuthorization(ParkingAuthorization.SystemAdminPolicy)
        .WithName("ApproveLayoutChange")
        .Produces<ReviewedLayoutChange>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost(reviewRoute + "/reject", async (Guid lotId, Guid requestId, RejectLayoutRequest request,
            ICurrentUser currentUser, HttpContext http, ISender sender, CancellationToken ct) =>
        {
            if (!Guid.TryParse(currentUser.SubjectId, out var adminId)) return Results.Forbid();
            var result = await sender.Send(new ReviewLayoutChangeCommand(lotId, requestId, adminId, false, request.Reason), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error!, http);
        })
        .RequireAuthorization(ParkingAuthorization.SystemAdminPolicy)
        .WithName("RejectLayoutChange")
        .Produces<ReviewedLayoutChange>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
        return app;
    }

    private static IResult ToProblem(Error error, HttpContext context)
    {
        var status = error == LayoutReviewErrors.NotFound ? StatusCodes.Status404NotFound
            : error.Code == "parking.layout.invalid" || error == LayoutReviewErrors.InvalidDecision
                ? StatusCodes.Status400BadRequest
            : StatusCodes.Status409Conflict;
        return BusinessProblem.Create(context, status, error);
    }
}
