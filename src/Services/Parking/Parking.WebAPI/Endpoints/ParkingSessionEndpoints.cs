using System.ComponentModel.DataAnnotations;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
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
        [property: Required, AllowedValues(nameof(VehicleType.Car), nameof(VehicleType.Motorbike))] string? VehicleType);

    public sealed record CancelParkingSessionRequest(
        [property: Required, StringLength(ParkingSession.CancelReasonMaxLength, MinimumLength = 1)] string? Reason);

    public static IEndpointRouteBuilder MapParkingSessionEndpoints(this IEndpointRouteBuilder app)
    {
        // US-PARK-003. Staff or gate only; 401 without a token and 403 for a token without the Staff role.
        // Validation failures become 400 in the shared exception handler.
        app.MapPost(Route, async (OpenParkingSessionRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new OpenParkingSessionCommand(request.LotId, request.PlateNumber ?? string.Empty, request.VehicleType ?? string.Empty),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Created($"{Route}/{result.Value!.SessionId}", result.Value)
                    : Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: result.Error!.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("OpenParkingSession")
            .WithSummary("Open a parking session")
            .Produces<OpenedParkingSession>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        // Staff undo a session when the barrier was opened but the vehicle did not enter (lead decision 2026-10-06).
        app.MapPost($"{Route}/{{sessionId:guid}}/cancel", async (
                Guid sessionId,
                CancelParkingSessionRequest request,
                ICurrentUser currentUser,
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
                    : StatusCodes.Status409Conflict;
                return Results.Problem(
                    statusCode: statusCode,
                    title: result.Error!.Message,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
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
