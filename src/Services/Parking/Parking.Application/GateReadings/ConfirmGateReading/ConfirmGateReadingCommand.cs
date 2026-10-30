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

namespace ParkingSystem.Parking.Application.GateReadings.ConfirmGateReading;

// US-PARK-002 (BR-05.a, MF-08/09A/09B): the recognition was not sure enough, so staff look at the vehicle and confirm the
// plate and vehicle type, keeping or correcting what the device read. This resolves the LowConfidencePlate incident and is
// audited (MAIN FLOW: "Thao tác xác minh thủ công được ghi audit log"). Opening the session stays a separate step.
public sealed record ConfirmGateReadingCommand(Guid IncidentId, string PlateNumber, string VehicleType, Guid ActorUserId)
    : IRequest<Result<ConfirmedGateReading>>;

public sealed record ConfirmedGateReading(
    Guid IncidentId,
    Guid LotId,
    string PlateNumber,
    string VehicleType,
    string Decision,
    bool PlateFormatValid,
    DateTimeOffset ConfirmedAt);

public static class ConfirmGateReadingErrors
{
    // Also returned for an incident of another type: this endpoint only handles plate confirmations.
    public static readonly Error ReadingNotFound =
        new("parking.gate_reading.not_found", "The gate reading does not exist.");

    // Another staff member has just confirmed it.
    public static readonly Error NotPending =
        new("parking.gate_reading.not_pending", "This gate reading is not waiting for staff confirmation.");
}

public sealed class ConfirmGateReadingCommandValidator : AbstractValidator<ConfirmGateReadingCommand>
{
    public ConfirmGateReadingCommandValidator()
    {
        RuleFor(command => command.IncidentId).NotEmpty();
        RuleFor(command => command.ActorUserId).NotEmpty();

        // Staff confirm what they see, so a plate is required; its format is not enforced (decision 2026-10-29).
        RuleFor(command => command.PlateNumber)
            .Must(plate => !string.IsNullOrWhiteSpace(plate))
            .MaximumLength(ParkingSession.PlateNumberMaxLength);

        RuleFor(command => command.VehicleType)
            .Must(value => Enum.GetNames<VehicleType>().Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"vehicleType must be one of: {string.Join(", ", Enum.GetNames<VehicleType>())}.");
    }
}

public sealed class ConfirmGateReadingCommandHandler(
    IIncidentRepository incidents,
    IParkingLotAccess lotAccess,
    IAuditLog auditLog,
    IClock clock) : IRequestHandler<ConfirmGateReadingCommand, Result<ConfirmedGateReading>>
{
    public async Task<Result<ConfirmedGateReading>> Handle(ConfirmGateReadingCommand request, CancellationToken cancellationToken)
    {
        var incident = await incidents.FindByIdAsync(request.IncidentId, cancellationToken);
        if (incident is null || incident.IncidentType != IncidentType.LowConfidencePlate)
            return Result<ConfirmedGateReading>.Failure(ConfirmGateReadingErrors.ReadingNotFound);

        // TECH-12: the lot comes from the incident, not from the client.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, incident.LotId, cancellationToken))
            return Result<ConfirmedGateReading>.Failure(ParkingLotAccessErrors.AccessDenied);

        var plate = ParkingSession.NormalizePlate(request.PlateNumber);
        var vehicleType = Enum.Parse<VehicleType>(request.VehicleType, ignoreCase: true);
        var now = clock.UtcNow;

        // The ERD has no column for the confirmed plate: it is kept in resolution_reason, the observed values stay as read.
        if (!incident.Resolve(request.ActorUserId, now, $"Staff confirmed plate {plate} ({vehicleType})."))
            return Result<ConfirmedGateReading>.Failure(ConfirmGateReadingErrors.NotPending);

        var plateFormatValid = VietnamPlateFormat.IsValid(plate);
        // No plate in the audit details (NFR-15): only what kind of check it was.
        auditLog.Record(new AuditEntry(
            AuditActions.GateReadingConfirmed,
            request.ActorUserId,
            incident.LotId,
            nameof(Incident),
            incident.Id.ToString(),
            Reason: null,
            now,
            new Dictionary<string, string>
            {
                ["plateCorrected"] = (plate != (incident.ObservedPlate ?? string.Empty)).ToString(),
                ["vehicleTypeCorrected"] = (vehicleType != incident.ObservedVehicleType).ToString(),
                ["plateFormatValid"] = plateFormatValid.ToString()
            }));

        // One save: the resolution and its audit entry are committed together. Two staff at once: only the first wins.
        if (!await incidents.TrySaveChangesAsync(cancellationToken))
            return Result<ConfirmedGateReading>.Failure(ConfirmGateReadingErrors.NotPending);

        return Result<ConfirmedGateReading>.Success(new ConfirmedGateReading(
            incident.Id,
            incident.LotId,
            plate,
            vehicleType.ToString(),
            nameof(GateReadingDecision.StaffConfirmed),
            plateFormatValid,
            now));
    }
}
