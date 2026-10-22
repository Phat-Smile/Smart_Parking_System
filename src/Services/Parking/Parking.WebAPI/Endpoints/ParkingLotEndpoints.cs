using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.ParkingLots.ReviewParkingLot;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.GetParkingLotDetail;
using ParkingSystem.Parking.Application.ParkingLots.ListParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.RegisterParkingLot;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class ParkingLotEndpoints
{
    public sealed record RegisterParkingLotRequest(
        string? Name,
        string? Address,
        decimal? Latitude,
        decimal? Longitude,
        string? Type);

    public static IEndpointRouteBuilder MapParkingLotEndpoints(this IEndpointRouteBuilder app)
    {
        // US-OWNER-001: role is checked against the Identity JWT; owner_user_id comes from "sub", never from the request.
        app.MapPost("/api/parking/lots", async (
                RegisterParkingLotRequest request,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var ownerUserId) || ownerUserId == Guid.Empty)
                    return Results.Forbid();

                RegisteredParkingLot registered;
                try
                {
                    registered = await sender.Send(
                        new RegisterParkingLotCommand(
                            ownerUserId,
                            request.Name ?? string.Empty,
                            request.Address ?? string.Empty,
                            request.Latitude,
                            request.Longitude,
                            request.Type ?? string.Empty),
                        cancellationToken);
                }
                catch (DuplicateParkingLotException)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Parking lot already registered.",
                        detail: "This owner already has a parking lot with this name.",
                        extensions: new Dictionary<string, object?>
                        {
                            ["errorCode"] = "parking.lot_duplicate"
                        });
                }

                // Pending lots are intentionally hidden by GET /api/parking/lots/{id}.
                // Do not return a Location URL that would respond 404 until admin approval.
                return Results.Created((string?)null, registered);
            })
            .RequireAuthorization(ParkingAuthorization.ParkingLotOwnerPolicy)
            .WithName("RegisterParkingLot")
            .WithSummary("Register a parking lot for administrator review")
            .WithDescription("ParkingLotOwner only. New parking lots start Pending and are not visible in public catalog/detail.")
            .Produces<RegisteredParkingLot>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapGet("/api/parking/lots", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var parkingLots = await sender.Send(new ListParkingLotsQuery(), cancellationToken);
                return Results.Ok(parkingLots);
            })
            .AllowAnonymous()
            .WithName("ListParkingLots")
            .WithSummary("List approved parking lots")
            .WithDescription("Returns only parking lots that have been approved by an administrator. Authentication is not required.")
            .Produces<IReadOnlyList<ParkingLotCatalogItem>>(StatusCodes.Status200OK);

        app.MapGet("/api/parking/lots/{parkingLotId}", async (
                Guid parkingLotId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var parkingLot = await sender.Send(
                    new GetParkingLotDetailQuery(parkingLotId),
                    cancellationToken);

                return parkingLot is null
                    ? Results.NotFound()
                    : Results.Ok(parkingLot);
            })
            .AllowAnonymous()
            .WithName("GetParkingLotDetail")
            .WithSummary("Get an approved parking lot")
            .WithDescription("Returns the public parking-lot master-data detail for an approved parking lot. Pending and rejected parking lots are not exposed.")
            .Produces<ParkingLotDetail>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // US-ADMIN-002: authorized administrators can decide only Pending lots.
        app.MapPost("/api/parking/admin/lots/{parkingLotId:guid}/approve", async (
                Guid parkingLotId,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var actorId))
                    return Results.Forbid();

                var result = await sender.Send(new ReviewParkingLotCommand(
                    parkingLotId, ParkingLotApprovalStatus.Approved, null, actorId), cancellationToken);
                return ToReviewResult(result);
            })
            .RequireAuthorization(ParkingAuthorization.SystemAdminPolicy)
            .WithName("ApproveParkingLot")
            .WithSummary("Approve a pending parking lot")
            .Produces<ReviewedParkingLot>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("/api/parking/admin/lots/{parkingLotId:guid}/reject", async (
                Guid parkingLotId,
                RejectParkingLotRequest request,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var actorId))
                    return Results.Forbid();

                var result = await sender.Send(new ReviewParkingLotCommand(
                    parkingLotId, ParkingLotApprovalStatus.Rejected, request.Reason, actorId), cancellationToken);
                return ToReviewResult(result);
            })
            .RequireAuthorization(ParkingAuthorization.SystemAdminPolicy)
            .WithName("RejectParkingLot")
            .WithSummary("Reject a pending parking lot with a reason")
            .Produces<ReviewedParkingLot>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    public sealed record RejectParkingLotRequest(string? Reason);

    private static IResult ToReviewResult(ParkingSystem.BuildingBlocks.Results.Result<ReviewedParkingLot> result)
    {
        if (result.IsSuccess)
            return Results.Ok(result.Value);

        var statusCode = result.Error == ReviewParkingLotErrors.NotFound
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status409Conflict;
        return Results.Problem(
            statusCode: statusCode,
            title: result.Error!.Message,
            extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
    }
}
