using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions.FindOpenParkingSessionByPlate;

// US-STAFF-004 (BR-11.d): at the exit gate the QR is lost or does not match, so staff look the session up by plate.
// The QR token is not returned: a plate match is not proof of the session (BR-11.c); staff reissue the QR instead.
public sealed record FindOpenParkingSessionByPlateQuery(Guid LotId, string PlateNumber, Guid ActorUserId)
    : IRequest<Result<OpenParkingSessionByPlate>>;

public sealed record OpenParkingSessionByPlate(
    Guid SessionId,
    Guid LotId,
    string PlateNumber,
    string VehicleType,
    string CustomerType,
    string Status,
    DateTimeOffset EntryTime);

public static class FindOpenParkingSessionByPlateErrors
{
    public static readonly Error SessionNotFound =
        new("parking.session.not_found", "This plate has no open parking session in this lot.");
}

public sealed class FindOpenParkingSessionByPlateQueryValidator : AbstractValidator<FindOpenParkingSessionByPlateQuery>
{
    public FindOpenParkingSessionByPlateQueryValidator()
    {
        RuleFor(query => query.LotId).NotEmpty();
        RuleFor(query => query.PlateNumber)
            .Must(plate => !string.IsNullOrWhiteSpace(plate))
            .MaximumLength(ParkingSession.PlateNumberMaxLength);
        RuleFor(query => query.ActorUserId).NotEmpty();
    }
}

public sealed class FindOpenParkingSessionByPlateQueryHandler(IParkingSessionRepository sessions, IParkingLotAccess lotAccess)
    : IRequestHandler<FindOpenParkingSessionByPlateQuery, Result<OpenParkingSessionByPlate>>
{
    public async Task<Result<OpenParkingSessionByPlate>> Handle(FindOpenParkingSessionByPlateQuery request, CancellationToken cancellationToken)
    {
        // TECH-12: the lot comes from the client here, so access is checked before anything about its sessions is read.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, request.LotId, cancellationToken))
            return Result<OpenParkingSessionByPlate>.Failure(ParkingLotAccessErrors.AccessDenied);

        var session = await sessions.FindOpenByPlateAsync(
            request.LotId, ParkingSession.NormalizePlate(request.PlateNumber), cancellationToken);
        if (session is null)
            return Result<OpenParkingSessionByPlate>.Failure(FindOpenParkingSessionByPlateErrors.SessionNotFound);

        return Result<OpenParkingSessionByPlate>.Success(new OpenParkingSessionByPlate(
            session.Id,
            session.LotId,
            session.PlateNumber,
            session.VehicleType.ToString(),
            session.CustomerType.ToString(),
            session.Status.ToString(),
            session.EntryTime));
    }
}
