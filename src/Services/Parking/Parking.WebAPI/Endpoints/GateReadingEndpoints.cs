using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.GateReadings.RecordGateReading;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.GateReadings;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

// US-PARK-001. Sprint 2: staff type in what the gate device would send (no device or MQTT gate topic yet, context 11).
internal static class GateReadingEndpoints
{
    private const string Route = "/api/parking/gate-readings";

    // Fields stay nullable so a missing field reaches the validator and returns 400 with a clear message.
    public sealed record RecordGateReadingRequest(
        [property: Required] Guid LotId,
        [property: StringLength(GateReading.RawPlateMaxLength)]
        [property: Description("Plate as read at the gate; empty when no plate could be read (goes to staff).")]
        string? PlateNumber,
        [property: Required, AllowedValues(nameof(VehicleType.Car), nameof(VehicleType.Motorbike))]
        [property: Description("Car or Motorbike, case-insensitive.")]
        string? VehicleType,
        [property: Required, Range(0d, 1d)]
        [property: Description("Recognition confidence for the whole plate, from 0 to 1. 0.9 or more with a valid plate is accepted automatically.")]
        decimal? Confidence);

    public static IEndpointRouteBuilder MapGateReadingEndpoints(this IEndpointRouteBuilder app)
    {
        // 201 with the decision: AutoAccepted (staff may open the session) or PendingStaff (staff must confirm, US-PARK-002).
        app.MapPost(Route, async (
                RecordGateReadingRequest request,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(
                    new RecordGateReadingCommand(request.LotId, request.PlateNumber, request.VehicleType ?? string.Empty, request.Confidence, actorUserId),
                    cancellationToken);

                if (result.IsSuccess)
                    return Results.Created((string?)null, result.Value);

                var statusCode = result.Error == ParkingLotAccessErrors.AccessDenied
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status404NotFound;
                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("RecordGateReading")
            .WithSummary("Record a plate reading at the gate and decide whether staff must confirm it")
            .Produces<RecordedGateReading>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        return app;
    }
}
