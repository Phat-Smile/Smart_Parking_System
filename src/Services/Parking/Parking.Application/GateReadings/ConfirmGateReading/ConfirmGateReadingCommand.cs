using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.GateReadings;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.GateReadings.ConfirmGateReading;

// US-PARK-002 (BR-05.a, MF-08/09A/09B): the recognition was not sure enough, so staff look at the vehicle and confirm the
// plate and vehicle type, keeping or correcting what the device read. The check is audited (MAIN FLOW: "Thao tác xác minh
// thủ công được ghi audit log"). Opening the session stays a separate step, as for an AutoAccepted reading.
public sealed record ConfirmGateReadingCommand(Guid ReadingId, string PlateNumber, string VehicleType, Guid ActorUserId)
    : IRequest<Result<ConfirmedGateReading>>;

public sealed record ConfirmedGateReading(
    Guid ReadingId,
    Guid LotId,
    string PlateNumber,
    string VehicleType,
    string Decision,
    bool PlateFormatValid,
    DateTimeOffset ConfirmedAt);

public static class ConfirmGateReadingErrors
{
    public static readonly Error ReadingNotFound =
        new("parking.gate_reading.not_found", "The gate reading does not exist.");

    // An AutoAccepted reading, or one another staff member has just confirmed.
    public static readonly Error NotPending =
        new("parking.gate_reading.not_pending", "This gate reading is not waiting for staff confirmation.");
}

public sealed class ConfirmGateReadingCommandValidator : AbstractValidator<ConfirmGateReadingCommand>
{
    public ConfirmGateReadingCommandValidator()
    {
        RuleFor(command => command.ReadingId).NotEmpty();
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
    IGateReadingRepository readings,
    IParkingLotAccess lotAccess,
    IAuditLog auditLog,
    IClock clock) : IRequestHandler<ConfirmGateReadingCommand, Result<ConfirmedGateReading>>
{
    public async Task<Result<ConfirmedGateReading>> Handle(ConfirmGateReadingCommand request, CancellationToken cancellationToken)
    {
        var reading = await readings.FindByIdAsync(request.ReadingId, cancellationToken);
        if (reading is null)
            return Result<ConfirmedGateReading>.Failure(ConfirmGateReadingErrors.ReadingNotFound);

        // TECH-12: the lot comes from the reading, not from the client.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, reading.LotId, cancellationToken))
            return Result<ConfirmedGateReading>.Failure(ParkingLotAccessErrors.AccessDenied);

        var readPlate = reading.PlateNumber;
        var readType = reading.VehicleType;
        var now = clock.UtcNow;
        if (!reading.Confirm(request.PlateNumber, Enum.Parse<VehicleType>(request.VehicleType, ignoreCase: true), request.ActorUserId, now))
            return Result<ConfirmedGateReading>.Failure(ConfirmGateReadingErrors.NotPending);

        var plateFormatValid = VietnamPlateFormat.IsValid(reading.PlateNumber);
        // No plate in the audit details (NFR-15): only what kind of check it was.
        auditLog.Record(new AuditEntry(
            AuditActions.GateReadingConfirmed,
            request.ActorUserId,
            reading.LotId,
            nameof(GateReading),
            reading.Id.ToString(),
            Reason: null,
            now,
            new Dictionary<string, string>
            {
                ["plateCorrected"] = (reading.PlateNumber != readPlate).ToString(),
                ["vehicleTypeCorrected"] = (reading.VehicleType != readType).ToString(),
                ["plateFormatValid"] = plateFormatValid.ToString()
            }));

        // One save: the confirmation and its audit entry are committed together. Two staff at once: only the first wins.
        if (!await readings.TrySaveChangesAsync(cancellationToken))
            return Result<ConfirmedGateReading>.Failure(ConfirmGateReadingErrors.NotPending);

        return Result<ConfirmedGateReading>.Success(new ConfirmedGateReading(
            reading.Id,
            reading.LotId,
            reading.PlateNumber,
            reading.VehicleType.ToString(),
            reading.Decision.ToString(),
            plateFormatValid,
            now));
    }
}
