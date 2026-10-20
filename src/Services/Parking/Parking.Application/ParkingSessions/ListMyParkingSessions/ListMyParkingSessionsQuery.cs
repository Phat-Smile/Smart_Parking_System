using MediatR;

namespace ParkingSystem.Parking.Application.ParkingSessions.ListMyParkingSessions;

// US-PARK-009: a signed-in customer sees the QR of their own open sessions (BR-11.b, BR-05.f).
public sealed record ListMyParkingSessionsQuery(Guid UserId) : IRequest<IReadOnlyList<MyParkingSession>>;

public sealed record MyParkingSession(
    Guid SessionId,
    Guid LotId,
    string PlateNumber,
    string VehicleType,
    string Status,
    DateTimeOffset EntryTime,
    string QrToken);

public sealed class ListMyParkingSessionsQueryHandler(IParkingSessionRepository sessions)
    : IRequestHandler<ListMyParkingSessionsQuery, IReadOnlyList<MyParkingSession>>
{
    public async Task<IReadOnlyList<MyParkingSession>> Handle(ListMyParkingSessionsQuery request, CancellationToken cancellationToken)
    {
        // Only Active / Paid sessions: the QR is invalid once the session is Completed or Cancelled (BR-11.e).
        var openSessions = await sessions.ListOpenByUserAsync(request.UserId, cancellationToken);

        return openSessions
            .Select(session => new MyParkingSession(
                session.Id,
                session.LotId,
                session.PlateNumber,
                session.VehicleType.ToString(),
                session.Status.ToString(),
                session.EntryTime,
                session.QrToken))
            .ToList();
    }
}
