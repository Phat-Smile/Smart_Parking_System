using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.GateReadings;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.GateReadings.RecordGateReading;

// US-PARK-001 (BR-05.a, MF-08/09A/09B): the gate device sends a plate, a vehicle type and a confidence; the system decides
// whether the vehicle is recognised automatically or staff must confirm it (US-PARK-002).
// The command does not know where the reading comes from: typed in by staff in Sprint 2, the MQTT gate event later.
public sealed record RecordGateReadingCommand(Guid LotId, string? PlateNumber, string VehicleType, decimal? Confidence, Guid ActorUserId)
    : IRequest<Result<RecordedGateReading>>;

public sealed record RecordedGateReading(
    Guid ReadingId,
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

        // An empty plate is allowed: "no plate read" goes to staff (MF-09B step 63).
        RuleFor(command => command.PlateNumber).MaximumLength(GateReading.RawPlateMaxLength);

        RuleFor(command => command.VehicleType)
            .Must(value => Enum.GetNames<VehicleType>().Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"vehicleType must be one of: {string.Join(", ", Enum.GetNames<VehicleType>())}.");

        RuleFor(command => command.Confidence)
            .NotNull()
            .InclusiveBetween(0m, 1m);
    }
}

public sealed class RecordGateReadingCommandHandler(
    IGateReadingRepository readings,
    IParkingLotDetailReader parkingLots,
    IParkingLotAccess lotAccess,
    IClock clock) : IRequestHandler<RecordGateReadingCommand, Result<RecordedGateReading>>
{
    public async Task<Result<RecordedGateReading>> Handle(RecordGateReadingCommand request, CancellationToken cancellationToken)
    {
        if (await parkingLots.GetApprovedByIdAsync(request.LotId, cancellationToken) is null)
            return Result<RecordedGateReading>.Failure(RecordGateReadingErrors.LotNotFound);

        // TECH-12: only staff or the owner assigned to the lot work its gate.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, request.LotId, cancellationToken))
            return Result<RecordedGateReading>.Failure(ParkingLotAccessErrors.AccessDenied);

        var reading = GateReading.Record(
            Guid.NewGuid(),
            request.LotId,
            request.PlateNumber,
            Enum.Parse<VehicleType>(request.VehicleType, ignoreCase: true),
            request.Confidence!.Value,
            clock.UtcNow,
            request.ActorUserId);
        readings.Add(reading);
        await readings.SaveChangesAsync(cancellationToken);

        return Result<RecordedGateReading>.Success(new RecordedGateReading(
            reading.Id,
            reading.LotId,
            reading.PlateNumber,
            reading.VehicleType.ToString(),
            reading.Confidence,
            reading.Decision.ToString(),
            reading.ReceivedAt));
    }
}
