using System.Buffers.Text;
using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Identity;
using ParkingSystem.Parking.Application.Common.Persistence;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions.OpenParkingSession;

public sealed record OpenParkingSessionCommand(Guid LotId, string PlateNumber, string VehicleType, Guid ActorUserId)
    : IRequest<Result<OpenedParkingSession>>;

public sealed record OpenedParkingSession(
    Guid SessionId,
    Guid LotId,
    string PlateNumber,
    string VehicleType,
    string CustomerType,
    string Status,
    DateTimeOffset EntryTime,
    bool IsAccountLinked,
    Guid? ReservationId,
    string QrToken,
    Guid? ZoneId,
    int? AvailableMotorbikeSpaces);

public static class OpenParkingSessionErrors
{
    // ASSUMPTION: one open (Active or Paid) session per plate per lot; not written in Context yet.
    public static readonly Error SessionAlreadyOpen =
        new("parking.session.already_open", "This plate already has an open session in this lot.");

    // Unknown and not-approved lots share one error, like the public lot detail API (PR #31 review).
    public static readonly Error LotNotFound =
        new("parking.lot.not_found", "The parking lot does not exist or is not approved.");

    // US-PARK-008 (BR-07.b): the motorbike zone has reached its capacity, so the barrier stays closed.
    public static readonly Error MotorbikeZoneFull =
        new("parking.motorbike_zone.full", "The motorbike zone of this lot is full.");

    public static readonly Error MotorbikeZoneNotConfigured =
        new("parking.motorbike_zone.not_configured", "This lot has no motorbike zone.");
}

public sealed class OpenParkingSessionCommandValidator : AbstractValidator<OpenParkingSessionCommand>
{
    public OpenParkingSessionCommandValidator()
    {
        RuleFor(command => command.LotId).NotEmpty();
        RuleFor(command => command.ActorUserId).NotEmpty();

        RuleFor(command => command.PlateNumber)
            .Must(plate => !string.IsNullOrWhiteSpace(plate))
            .MaximumLength(ParkingSession.PlateNumberMaxLength);

        // Match names only: Enum.TryParse also accepts numeric strings ("1") and padded values (" Car ").
        RuleFor(command => command.VehicleType)
            .Must(value => Enum.GetNames<VehicleType>().Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"vehicleType must be one of: {string.Join(", ", Enum.GetNames<VehicleType>())}.");
    }
}

/// <summary>
/// One step of the gate-entry flow (MAIN FLOW MF-08, MF-09A, MF-09B), not the whole entry. Called by staff or the gate
/// only after these steps are done elsewhere:
/// <list type="bullet">
/// <item>The plate is accepted: confidence at least 0.9 and a valid format, or confirmed by staff (BR-05.a).</item>
/// <item>Car with a booking (MF-08): Booking has moved the booking to Used, and a slot is designated (US-PARK-004).</item>
/// <item>Walk-in car (MF-09A): walk-in capacity is above 0 and a slot is designated atomically (US-PARK-007).</item>
/// </list>
/// Motorbikes (MF-09B, US-PARK-008) take one space of the lot's motorbike zone here, atomically and in the same
/// transaction as the session; a full zone keeps the barrier closed.
/// Opening the session is the moment the system sends the barrier-open command, so EntryTime is taken from the clock here.
/// </summary>
public sealed class OpenParkingSessionCommandHandler(
    IParkingSessionRepository sessions,
    IParkingLotDetailReader parkingLots,
    IParkingLotAccess lotAccess,
    IPlateOwnerLookup plateOwners,
    IClock clock,
    IMotorbikeZoneCounter motorbikeZones,
    IParkingUnitOfWork unitOfWork) : IRequestHandler<OpenParkingSessionCommand, Result<OpenedParkingSession>>
{
    public async Task<Result<OpenedParkingSession>> Handle(OpenParkingSessionCommand request, CancellationToken cancellationToken)
    {
        // Only an existing, approved lot can receive vehicles.
        if (await parkingLots.GetApprovedByIdAsync(request.LotId, cancellationToken) is null)
            return Result<OpenedParkingSession>.Failure(OpenParkingSessionErrors.LotNotFound);

        // TECH-12: chỉ Staff/Owner được gắn với bãi này mới mở phiên được.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, request.LotId, cancellationToken))
            return Result<OpenedParkingSession>.Failure(ParkingLotAccessErrors.AccessDenied);

        var plate = ParkingSession.NormalizePlate(request.PlateNumber);
        var vehicleType = Enum.Parse<VehicleType>(request.VehicleType, ignoreCase: true);

        if (await sessions.HasOpenSessionAsync(request.LotId, plate, cancellationToken))
            return Result<OpenedParkingSession>.Failure(OpenParkingSessionErrors.SessionAlreadyOpen);

        // A plate match links the session for history and fees only; it is not authentication (BR-05.f).
        var owner = await plateOwners.FindByPlateAsync(plate, cancellationToken);

        // Taking the motorbike space and saving the session commit together: a failed save gives the space back.
        await using var transaction = vehicleType == VehicleType.Motorbike
            ? await unitOfWork.BeginTransactionAsync(cancellationToken)
            : null;

        MotorbikeSpaceTaken? space = null;
        if (vehicleType == VehicleType.Motorbike)
        {
            // US-PARK-008: check and +1 in one statement, so two gates can never both take the last space.
            space = await motorbikeZones.TakeSpaceAsync(request.LotId, cancellationToken);
            if (space is null)
                return Result<OpenedParkingSession>.Failure(
                    await motorbikeZones.GetAvailabilityAsync(request.LotId, cancellationToken) is null
                        ? OpenParkingSessionErrors.MotorbikeZoneNotConfigured
                        : OpenParkingSessionErrors.MotorbikeZoneFull);
        }

        var session = ParkingSession.Open(
            Guid.NewGuid(),
            request.LotId,
            plate,
            vehicleType,
            clock.UtcNow,
            NewQrToken(),
            owner?.UserId,
            owner?.VehicleId,
            space?.ZoneId);
        sessions.Add(session);

        // Two entry requests for the same plate at the same time: the unique index lets only one win.
        if (!await sessions.TrySaveChangesAsync(cancellationToken))
            return Result<OpenedParkingSession>.Failure(OpenParkingSessionErrors.SessionAlreadyOpen);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Result<OpenedParkingSession>.Success(new OpenedParkingSession(
            session.Id,
            session.LotId,
            session.PlateNumber,
            session.VehicleType.ToString(),
            session.CustomerType.ToString(),
            session.Status.ToString(),
            session.EntryTime,
            owner is not null,
            session.ReservationId,
            session.QrToken,
            session.ZoneId,
            space?.AvailableSpaces));
    }

    // BR-11.b: random token with no account id or plate inside, from a cryptographic generator.
    private static string NewQrToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ParkingSession.QrTokenByteLength));
}
