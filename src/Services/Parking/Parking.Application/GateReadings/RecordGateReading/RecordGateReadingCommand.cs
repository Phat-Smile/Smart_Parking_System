using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.Incidents;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.GateReadings;
using ParkingSystem.Parking.Domain.Incidents;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.GateReadings.RecordGateReading;

// US-PARK-001 (BR-05.a, MF-08/09A/09B): the gate device sends a plate, a vehicle type and a confidence; the system decides
// whether the vehicle is recognised automatically or staff must confirm it (US-PARK-002).
// The command does not know where the reading comes from: typed in by staff in Sprint 2, the MQTT gate event later.
public sealed record RecordGateReadingCommand(Guid LotId, string? PlateNumber, string VehicleType, decimal? Confidence, Guid ActorUserId)
    : IRequest<Result<RecordedGateReading>>;

/// <param name="IncidentId">The LowConfidencePlate incident opened for staff; null when the reading was accepted automatically.</param>
public sealed record RecordedGateReading(
    Guid? IncidentId,
    Guid LotId,
    string PlateNumber,
    string VehicleType,
    decimal Confidence,
    string Decision,
    DateTimeOffset ReceivedAt);

public static class RecordGateReadingErrors
{
    // Same code as opening a session: unknown and not-approved lots look the same.
    public static readonly Error LotNotFound =
        new("parking.lot.not_found", "The parking lot does not exist or is not approved.");
}

public sealed class RecordGateReadingCommandValidator : AbstractValidator<RecordGateReadingCommand>
{
    public RecordGateReadingCommandValidator()
    {
        RuleFor(command => command.LotId).NotEmpty();
        RuleFor(command => command.ActorUserId).NotEmpty();

        // An empty plate is allowed: "no plate read" goes to staff (MF-09B step 63). ERD observed_plate is varchar(20).
        RuleFor(command => command.PlateNumber).MaximumLength(ParkingSession.PlateNumberMaxLength);

        RuleFor(command => command.VehicleType)
            .Must(value => Enum.GetNames<VehicleType>().Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"vehicleType must be one of: {string.Join(", ", Enum.GetNames<VehicleType>())}.");

        RuleFor(command => command.Confidence)
            .NotNull()
            .InclusiveBetween(0m, 1m);
    }
}

public sealed class RecordGateReadingCommandHandler(
    IIncidentRepository incidents,
    IParkingLotDetailReader parkingLots,
    IParkingLotAccess lotAccess,
    IAuditLog auditLog,
    IClock clock) : IRequestHandler<RecordGateReadingCommand, Result<RecordedGateReading>>
{
    public async Task<Result<RecordedGateReading>> Handle(RecordGateReadingCommand request, CancellationToken cancellationToken)
    {
        if (await parkingLots.GetApprovedByIdAsync(request.LotId, cancellationToken) is null)
            return Result<RecordedGateReading>.Failure(RecordGateReadingErrors.LotNotFound);

        // TECH-12: only staff or the owner assigned to the lot work its gate.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, request.LotId, cancellationToken))
            return Result<RecordedGateReading>.Failure(ParkingLotAccessErrors.AccessDenied);

        var plate = string.IsNullOrWhiteSpace(request.PlateNumber) ? string.Empty : ParkingSession.NormalizePlate(request.PlateNumber);
        var vehicleType = Enum.Parse<VehicleType>(request.VehicleType, ignoreCase: true);
        var confidence = request.Confidence!.Value;
        var decision = GateReadingRules.Decide(plate, confidence);
        var now = clock.UtcNow;

        // A reading staff must confirm becomes an open incident in the staff queue (MF-14).
        Guid? incidentId = null;
        if (decision == GateReadingDecision.PendingStaff)
        {
            var incident = Incident.OpenLowConfidencePlate(Guid.NewGuid(), request.LotId, plate, confidence, vehicleType, now);
            incidents.Add(incident);
            incidentId = incident.Id;
        }
        else
        {
            // Lead review of #151: an automatic pass leaves a trace (who sent it, which lot, when). No plate in the details
            // (NFR-15); the plate is on the parking session staff open next.
            auditLog.Record(new AuditEntry(
                AuditActions.GateReadingAutoAccepted,
                request.ActorUserId,
                request.LotId,
                "GateReading",
                Guid.NewGuid().ToString(),
                Reason: null,
                now,
                new Dictionary<string, string>
                {
                    ["vehicleType"] = vehicleType.ToString(),
                    ["confidence"] = confidence.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }));
        }

        // One save: the incident or the audit entry.
        await incidents.SaveChangesAsync(cancellationToken);

        return Result<RecordedGateReading>.Success(new RecordedGateReading(
            incidentId,
            request.LotId,
            plate,
            vehicleType.ToString(),
            confidence,
            decision.ToString(),
            now));
    }
}
