using System.Buffers.Text;
using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Identity;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions.OpenParkingSession;

public sealed record OpenParkingSessionCommand(Guid LotId, string PlateNumber, string VehicleType)
    : IRequest<Result<OpenedParkingSession>>;

public sealed record OpenedParkingSession(
    Guid SessionId,
    Guid LotId,
    string PlateNumber,
    string VehicleType,
    string Status,
    DateTimeOffset EnteredAt,
    bool IsAccountLinked,
    Guid? BookingId,
    string QrToken);

public static class OpenParkingSessionErrors
{
    // ASSUMPTION: one open (Active or Paid) session per plate per lot; not written in Context yet.
    public static readonly Error SessionAlreadyOpen =
        new("parking.session.already_open", "This plate already has an open session in this lot.");
}

public sealed class OpenParkingSessionCommandValidator : AbstractValidator<OpenParkingSessionCommand>
{
    public OpenParkingSessionCommandValidator()
    {
        RuleFor(command => command.LotId).NotEmpty();

        RuleFor(command => command.PlateNumber)
            .Must(plate => !string.IsNullOrWhiteSpace(plate))
            .MaximumLength(ParkingSession.PlateNumberMaxLength);

        // Enum.IsDefined is needed because Enum.TryParse also accepts numeric strings such as "5".
        RuleFor(command => command.VehicleType)
            .Must(value => Enum.TryParse<VehicleType>(value, ignoreCase: true, out var type) && Enum.IsDefined(type))
            .WithMessage($"vehicleType must be one of: {string.Join(", ", Enum.GetNames<VehicleType>())}.");
    }
}

public sealed class OpenParkingSessionCommandHandler(
    IParkingSessionRepository sessions,
    IPlateOwnerLookup plateOwners,
    IClock clock) : IRequestHandler<OpenParkingSessionCommand, Result<OpenedParkingSession>>
{
    public async Task<Result<OpenedParkingSession>> Handle(OpenParkingSessionCommand request, CancellationToken cancellationToken)
    {
        var plate = ParkingSession.NormalizePlate(request.PlateNumber);
        var vehicleType = Enum.Parse<VehicleType>(request.VehicleType, ignoreCase: true);

        if (await sessions.HasOpenSessionAsync(request.LotId, plate, cancellationToken))
            return Result<OpenedParkingSession>.Failure(OpenParkingSessionErrors.SessionAlreadyOpen);

        // A plate match links the session for history and fees only; it is not authentication (BR-05.f).
        var owner = await plateOwners.FindByPlateAsync(plate, cancellationToken);

        var session = ParkingSession.Open(
            Guid.NewGuid(),
            request.LotId,
            plate,
            vehicleType,
            clock.UtcNow,
            NewQrToken(),
            owner?.UserId,
            owner?.VehicleId);
        sessions.Add(session);

        // Two entry requests for the same plate at the same time: the unique index lets only one win.
        if (!await sessions.TrySaveChangesAsync(cancellationToken))
            return Result<OpenedParkingSession>.Failure(OpenParkingSessionErrors.SessionAlreadyOpen);

        return Result<OpenedParkingSession>.Success(new OpenedParkingSession(
            session.Id,
            session.LotId,
            session.PlateNumber,
            session.VehicleType.ToString(),
            session.Status.ToString(),
            session.EnteredAt,
            owner is not null,
            session.BookingId,
            session.QrToken));
    }

    // BR-11.b: random token with no account id or plate inside, from a cryptographic generator.
    private static string NewQrToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ParkingSession.QrTokenByteLength));
}
