using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions.GetParkingSession;

// Lead task S2 (2026-10-27, replaces DOC-006 "no GET /{id}"): the detail of one session for the customer who owns it
// (customer QR page) or for staff assigned to its lot.
public sealed record GetParkingSessionQuery(Guid SessionId, Guid CallerId) : IRequest<Result<ParkingSessionDetail>>;

// Decision 2026-10-27: SlotCode stays null until US-PARK-007 stores the car slot; PaidAtUtc and GraceUntilUtc stay null
// until US-PAY-006 marks sessions Paid (GraceUntilUtc = PaidAtUtc + 15 minutes, BR-09.d).
public sealed record ParkingSessionDetail(
    Guid SessionId,
    Guid LotId,
    string PlateNumber,
    string VehicleType,
    string Status,
    DateTimeOffset EntryTime,
    string? SlotCode,
    string? QrToken,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset? GraceUntilUtc);

public static class GetParkingSessionErrors
{
    public static readonly Error SessionNotFound =
        new("parking.session.not_found", "The parking session does not exist.");
}

public sealed class GetParkingSessionQueryHandler(IParkingSessionRepository sessions, IParkingLotAccess lotAccess)
    : IRequestHandler<GetParkingSessionQuery, Result<ParkingSessionDetail>>
{
    public async Task<Result<ParkingSessionDetail>> Handle(GetParkingSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await sessions.FindByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result<ParkingSessionDetail>.Failure(GetParkingSessionErrors.SessionNotFound);

        // TECH-12: a caller who does not own the session must be assigned to its lot; the lot comes from the session.
        var isOwner = session.UserId == request.CallerId;
        if (!isOwner && !await lotAccess.CanOperateAsync(request.CallerId, session.LotId, cancellationToken))
            return Result<ParkingSessionDetail>.Failure(ParkingLotAccessErrors.AccessDenied);

        // Only the owner gets the QR (BR-11.b), and only while the session is open: a closed session's QR is invalid (BR-11.e).
        var qrToken = isOwner && ParkingSession.OpenStatuses.Contains(session.Status) ? session.QrToken : null;

        return Result<ParkingSessionDetail>.Success(new ParkingSessionDetail(
            session.Id,
            session.LotId,
            session.PlateNumber,
            session.VehicleType.ToString(),
            session.Status.ToString(),
            session.EntryTime,
            SlotCode: null,
            qrToken,
            PaidAtUtc: null,
            GraceUntilUtc: null));
    }
}
