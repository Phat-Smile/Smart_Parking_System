using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions.CancelParkingSession;
using ParkingSystem.Parking.Application.ParkingSessions.OpenParkingSession;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class ParkingSessionEndpoints
{
    private const string Route = "/api/parking/sessions";

    // Fields stay nullable so a missing field reaches the validator and returns 400 with a clear message.
    // The attributes only describe the contract in Swagger (DOC-002); the FluentValidation validators enforce it.
    public sealed record OpenParkingSessionRequest(
        [property: Required] Guid LotId,
        [property: Required, StringLength(ParkingSession.PlateNumberMaxLength, MinimumLength = 1)] string? PlateNumber,
        [property: Required, AllowedValues(nameof(VehicleType.Car), nameof(VehicleType.Motorbike))]
        [property: Description("Car or Motorbike, case-insensitive (car and MOTORBIKE are accepted).")]
        string? VehicleType);

    public sealed record CancelParkingSessionRequest(
        [property: Required, StringLength(ParkingSession.CancelReasonMaxLength, MinimumLength = 1)] string? Reason);

    public static IEndpointRouteBuilder MapParkingSessionEndpoints(this IEndpointRouteBuilder app)
    {
        // US-PARK-003. Staff or gate only; 401 without a token, 403 without the Staff role or for a lot the caller is not assigned to.
        // Validation failures become 400 in the shared exception handler.
        app.MapPost(Route, async (
                OpenParkingSessionRequest request,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                // Cần biết ai mở phiên để kiểm người đó có được gắn với bãi không (TECH-12).
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(
                    new OpenParkingSessionCommand(request.LotId, request.PlateNumber ?? string.Empty, request.VehicleType ?? string.Empty, actorUserId),
                    cancellationToken);

                // Location points to GET /{id}, which is not built yet (lead decision 2026-10-08, DOC-006).
                if (result.IsSuccess)
                    return Results.Created($"{Route}/{result.Value!.SessionId}", result.Value);

                var statusCode = result.Error == OpenParkingSessionErrors.LotNotFound
                    ? StatusCodes.Status404NotFound
                    : result.Error == ParkingLotAccessErrors.AccessDenied
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status409Conflict;
                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("OpenParkingSession")
            .WithSummary("Open a parking session")
            .Produces<OpenedParkingSession>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        // Staff undo a session when the barrier was opened but the vehicle did not enter (lead decision 2026-10-06).
        app.MapPost($"{Route}/{{sessionId:guid}}/cancel", async (
                Guid sessionId,
                CancelParkingSessionRequest request,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                // ERD: actor_user_id is identity.users.id, so the token subject must be a uuid.
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(
                    new CancelParkingSessionCommand(sessionId, request.Reason ?? string.Empty, actorUserId),
                    cancellationToken);

                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = result.Error == CancelParkingSessionErrors.SessionNotFound
                    ? StatusCodes.Status404NotFound
                    : result.Error == ParkingLotAccessErrors.AccessDenied
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status409Conflict;
                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("CancelParkingSession")
            .WithSummary("Cancel a parking session when the vehicle did not enter")
            .Produces<CancelledParkingSession>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        return app;
    }
}
