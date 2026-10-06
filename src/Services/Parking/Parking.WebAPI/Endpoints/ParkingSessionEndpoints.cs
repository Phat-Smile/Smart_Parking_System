using MediatR;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Parking.Application.ParkingSessions.OpenParkingSession;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

internal static class ParkingSessionEndpoints
{
    private const string Route = "/api/parking/sessions";

    public sealed record OpenParkingSessionRequest(Guid LotId, string? PlateNumber, string? VehicleType);

    public static IEndpointRouteBuilder MapParkingSessionEndpoints(this IEndpointRouteBuilder app)
    {
        // US-PARK-003. Staff/LotOwner roles are added when Identity has roles; any signed-in user for now.
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
            .RequireAuthenticatedUser()
            .WithName("OpenParkingSession")
            .WithSummary("Open a parking session")
            .Produces<OpenedParkingSession>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
