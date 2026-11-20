using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using MediatR;
using ParkingSystem.AspNetCore;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.GateReadings.ConfirmGateReading;
using ParkingSystem.Parking.Application.GateReadings.ListPendingGateReadings;
using ParkingSystem.Parking.Application.GateReadings.RecordGateReading;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Domain.Incidents;
using ParkingSystem.Parking.WebAPI.Security;

namespace ParkingSystem.Parking.WebAPI.Endpoints;

// US-PARK-001: staff type in what the gate device would send in Sprint 2 (no device or MQTT gate topic yet, context 11).
// US-PARK-002: staff confirm the readings that were not accepted automatically (open LowConfidencePlate incidents).
internal static class GateReadingEndpoints
{
    private const string Route = "/api/parking/gate-readings";

    // Fields stay nullable so a missing field reaches the validator and returns 400 with a clear message.
    public sealed record RecordGateReadingRequest(
        [property: Required] Guid LotId,
        [property: StringLength(ParkingSession.PlateNumberMaxLength)]
        [property: Description("Plate as read at the gate; empty when no plate could be read (goes to staff).")]
        string? PlateNumber,
        [property: Required, AllowedValues(nameof(VehicleType.Car), nameof(VehicleType.Motorbike))]
        [property: Description("Car or Motorbike, case-insensitive.")]
        string? VehicleType,
        [property: Required, Range(0d, 1d)]
        [property: Description("Recognition confidence for the whole plate, from 0 to 1. 0.9 or more with a valid plate is accepted automatically.")]
        decimal? Confidence);

    public sealed record ConfirmGateReadingRequest(
        [property: Required, StringLength(ParkingSession.PlateNumberMaxLength, MinimumLength = 1)]
        [property: Description("The plate staff see on the vehicle: the one read, or a corrected one.")]
        string? PlateNumber,
        [property: Required, AllowedValues(nameof(VehicleType.Car), nameof(VehicleType.Motorbike))]
        [property: Description("Car or Motorbike, case-insensitive.")]
        string? VehicleType);

    // BE-PK02: staff can confirm an existing pending gate incident, not invent a capture timestamp.
    // The original incident.OccurredAt stays immutable and remains the Booking arrival evidence.
    public sealed record OverrideArrivalRequest(
        [property: Required] Guid IncidentId,
        [property: Required] Guid LotId,
        [property: Required, StringLength(ParkingSession.PlateNumberMaxLength, MinimumLength = 1)] string? PlateNumber,
        [property: Required, AllowedValues(nameof(VehicleType.Car), nameof(VehicleType.Motorbike))] string? VehicleType,
        [property: Required, StringLength(Incident.ResolutionReasonMaxLength, MinimumLength = 1)] string? Reason);

    public static IEndpointRouteBuilder MapGateReadingEndpoints(this IEndpointRouteBuilder app)
    {
        // BE-PK02: explicit manual arrival confirmation, isolated from session/barrier actions.
        // No request timestamp is accepted; an authenticated staff member must reference a pending incident.
        app.MapPost("/api/parking/staff/gate/override-arrival", async (
                OverrideArrivalRequest request,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                if (request.IncidentId == Guid.Empty || request.LotId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > Incident.ResolutionReasonMaxLength)
                    return Results.BadRequest(new { message = "incidentId, lotId and a reason of up to 500 characters are required." });

                var result = await sender.Send(new ConfirmGateReadingCommand(
                    request.IncidentId,
                    request.PlateNumber ?? string.Empty,
                    request.VehicleType ?? string.Empty,
                    actorUserId,
                    ExpectedLotId: request.LotId,
                    OverrideReason: request.Reason), cancellationToken);

                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = result.Error == ConfirmGateReadingErrors.ReadingNotFound
                    ? StatusCodes.Status404NotFound
                    : result.Error == ParkingLotAccessErrors.AccessDenied
                        ? StatusCodes.Status403Forbidden
                        : result.Error == ConfirmGateReadingErrors.OverrideReasonRequired ||
                          result.Error == ConfirmGateReadingErrors.OverrideReasonTooLong
                            ? StatusCodes.Status400BadRequest
                            : StatusCodes.Status409Conflict;
                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("OverrideGateArrival")
            .WithSummary("Staff-confirm an existing gate incident with a reason; does not open a parking session")
            .Produces<ConfirmedGateReading>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // US-PARK-002. The staff queue of a lot: readings waiting for confirmation, oldest first.
        app.MapGet($"{Route}/pending", async (
                [Required] Guid? lotId,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(new ListPendingGateReadingsQuery(lotId ?? Guid.Empty, actorUserId), cancellationToken);
                return result.IsSuccess
                    ? Results.Ok(result.Value)
                    : BusinessProblem.Create(httpContext, StatusCodes.Status403Forbidden, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("ListPendingGateReadings")
            .WithSummary("List the gate readings of a lot waiting for staff confirmation")
            .Produces<IReadOnlyList<PendingGateReading>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // US-PARK-002. Staff confirm or correct the plate and vehicle type; 409 when it was not (or no longer) pending.
        app.MapPost($"{Route}/{{incidentId:guid}}/confirm", async (
                Guid incidentId,
                ConfirmGateReadingRequest request,
                ICurrentUser currentUser,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (!Guid.TryParse(currentUser.SubjectId, out var actorUserId))
                    return Results.Forbid();

                var result = await sender.Send(
                    new ConfirmGateReadingCommand(incidentId, request.PlateNumber ?? string.Empty, request.VehicleType ?? string.Empty, actorUserId),
                    cancellationToken);

                if (result.IsSuccess)
                    return Results.Ok(result.Value);

                var statusCode = result.Error == ConfirmGateReadingErrors.ReadingNotFound
                    ? StatusCodes.Status404NotFound
                    : result.Error == ParkingLotAccessErrors.AccessDenied
                        ? StatusCodes.Status403Forbidden
                        : StatusCodes.Status409Conflict;
                return BusinessProblem.Create(httpContext, statusCode, result.Error!);
            })
            .RequireAuthorization(ParkingAuthorization.StaffPolicy)
            .WithName("ConfirmGateReading")
            .WithSummary("Confirm or correct the plate of a gate reading waiting for staff")
            .Produces<ConfirmedGateReading>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        // 201 with the decision: AutoAccepted (staff may open the session) or PendingStaff with the incidentId staff must confirm.
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
                    new RecordGateReadingCommand(
                        request.LotId,
                        request.PlateNumber,
                        request.VehicleType ?? string.Empty,
                        request.Confidence,
                        actorUserId,
                        Source: "web"),
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
