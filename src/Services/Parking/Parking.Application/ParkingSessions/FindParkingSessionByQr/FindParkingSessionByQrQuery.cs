using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions.FindParkingSessionByQr;

// US-PARK-009: staff or the exit gate scan the session QR. The QR only identifies the session (BR-11.b);
// the exit decision itself (plate + QR + amount due = 0, BR-11.c) belongs to the exit flow (MF-12).
public sealed record FindParkingSessionByQrQuery(string QrToken, Guid ActorUserId) : IRequest<Result<ParkingSessionByQr>>;

public sealed record ParkingSessionByQr(
    Guid SessionId,
    Guid LotId,
    string PlateNumber,
    string VehicleType,
    string CustomerType,
    string Status,
    DateTimeOffset EntryTime);

public static class FindParkingSessionByQrErrors
{
    // Unknown and expired QR codes share one error so the API does not reveal whether an old session existed.
    public static readonly Error QrInvalid = new("parking.session.qr_invalid", "The QR code is not valid.");
}

public sealed class FindParkingSessionByQrQueryValidator : AbstractValidator<FindParkingSessionByQrQuery>
{
    public FindParkingSessionByQrQueryValidator()
    {
        RuleFor(query => query.QrToken)
            .Must(token => !string.IsNullOrWhiteSpace(token))
            .MaximumLength(ParkingSession.QrTokenMaxLength);
        RuleFor(query => query.ActorUserId).NotEmpty();
    }
}

public sealed class FindParkingSessionByQrQueryHandler(IParkingSessionRepository sessions, IParkingLotAccess lotAccess)
    : IRequestHandler<FindParkingSessionByQrQuery, Result<ParkingSessionByQr>>
{
    public async Task<Result<ParkingSessionByQr>> Handle(FindParkingSessionByQrQuery request, CancellationToken cancellationToken)
    {
        // BR-11.e: only Active / Paid sessions are returned, so a Completed or Cancelled session's QR is not found.
        var session = await sessions.FindOpenByQrTokenAsync(request.QrToken, cancellationToken);
        if (session is null)
            return Result<ParkingSessionByQr>.Failure(FindParkingSessionByQrErrors.QrInvalid);

        // TECH-12: staff only see sessions of a lot they are assigned to; the lot comes from the session, not the client.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, session.LotId, cancellationToken))
            return Result<ParkingSessionByQr>.Failure(ParkingLotAccessErrors.AccessDenied);

        return Result<ParkingSessionByQr>.Success(new ParkingSessionByQr(
            session.Id,
            session.LotId,
            session.PlateNumber,
            session.VehicleType.ToString(),
            session.CustomerType.ToString(),
            session.Status.ToString(),
            session.EntryTime));
    }
}
