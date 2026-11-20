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
// ExpectedLotId/OverrideReason are only set by the explicit staff arrival override endpoint.
// The original observed timestamp is immutable; confirmation never moves the booking deadline.
public sealed record ConfirmGateReadingCommand(
    Guid IncidentId, string PlateNumber, string VehicleType, Guid ActorUserId,
    Guid? ExpectedLotId = null, string? OverrideReason = null)
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

    public static readonly Error OverrideReasonRequired =
        new("parking.gate_reading.override_reason_required", "A reason is required to confirm arrival manually.");

    public static readonly Error OverrideReasonTooLong =
        new("parking.gate_reading.override_reason_too_long", "The manual arrival reason must be at most 500 characters.");
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

        When(command => command.ExpectedLotId.HasValue, () =>
        {
            RuleFor(command => command.ExpectedLotId)
                .Must(lotId => lotId.HasValue && lotId.Value != Guid.Empty);
            RuleFor(command => command.OverrideReason)
                .NotEmpty().WithMessage(ConfirmGateReadingErrors.OverrideReasonRequired.Message)
                .Must(reason => !string.IsNullOrWhiteSpace(reason))
                .WithMessage(ConfirmGateReadingErrors.OverrideReasonRequired.Message)
                .MaximumLength(Incident.ResolutionReasonMaxLength)
                .WithMessage(ConfirmGateReadingErrors.OverrideReasonTooLong.Message);
        });
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

        // Do not let the request claim an incident from a different lot. Fail without exposing its actual lot.
        if (request.ExpectedLotId is { } expectedLotId && incident.LotId != expectedLotId)
            return Result<ConfirmedGateReading>.Failure(ConfirmGateReadingErrors.ReadingNotFound);

        // Defense in depth for callers invoking the handler without the MediatR validation pipeline.
        if (request.ExpectedLotId.HasValue && string.IsNullOrWhiteSpace(request.OverrideReason))
            return Result<ConfirmedGateReading>.Failure(ConfirmGateReadingErrors.OverrideReasonRequired);
        if (request.OverrideReason is { Length: > Incident.ResolutionReasonMaxLength })
            return Result<ConfirmedGateReading>.Failure(ConfirmGateReadingErrors.OverrideReasonTooLong);

        // TECH-12: the lot comes from the incident, not from the client.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, incident.LotId, cancellationToken))
            return Result<ConfirmedGateReading>.Failure(ParkingLotAccessErrors.AccessDenied);

        var plate = ParkingSession.NormalizePlate(request.PlateNumber);
        var vehicleType = Enum.Parse<VehicleType>(request.VehicleType, ignoreCase: true);
        var now = clock.UtcNow;

        // The confirmed values go to their own columns; the observed values stay as read.
        if (!incident.ConfirmPlate(request.ActorUserId, now, plate, vehicleType))
            return Result<ConfirmedGateReading>.Failure(ConfirmGateReadingErrors.NotPending);

        var plateFormatValid = VietnamPlateFormat.IsValid(plate);
        // No plate in the audit details (NFR-15): only what kind of check it was.
        auditLog.Record(new AuditEntry(
            request.ExpectedLotId.HasValue ? AuditActions.GateArrivalOverride : AuditActions.GateReadingConfirmed,
            request.ActorUserId,
            incident.LotId,
            nameof(Incident),
            incident.Id.ToString(),
            Reason: request.ExpectedLotId.HasValue ? request.OverrideReason!.Trim() : null,
            now,
            new Dictionary<string, string>
            {
                ["arrivalTimestampSource"] = "incident.occurred_at",
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
