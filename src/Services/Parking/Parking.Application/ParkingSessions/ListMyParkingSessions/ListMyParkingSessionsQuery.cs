using MediatR;
using ParkingSystem.Parking.Application.ParkingLayout;

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
    string QrToken,
    string? ZoneCode,
    string? FloorName,
    string? SlotCode = null);

public sealed class ListMyParkingSessionsQueryHandler(
    IParkingSessionRepository sessions,
    IZoneLabelReader zoneLabels,
    ISlotDesignator slotDesignator)
    : IRequestHandler<ListMyParkingSessionsQuery, IReadOnlyList<MyParkingSession>>
{
    public async Task<IReadOnlyList<MyParkingSession>> Handle(ListMyParkingSessionsQuery request, CancellationToken cancellationToken)
    {
        // Only Active / Paid sessions: the QR is invalid once the session is Completed or Cancelled (BR-11.e).
        var openSessions = await sessions.ListOpenByUserAsync(request.UserId, cancellationToken);

        // US-PARK-008: a motorbike session shows the zone and floor the rider was sent to, next to the QR.
        var zoneIds = openSessions.Where(session => session.ZoneId is not null).Select(session => session.ZoneId!.Value).Distinct().ToList();
        var labels = await zoneLabels.GetLabelsAsync(zoneIds, cancellationToken);

        var sessionIds = openSessions.Select(session => session.Id).ToList();
        var slotCodes = await slotDesignator.FindSlotCodesAsync(sessionIds, cancellationToken);

        return openSessions
            .Select(session =>
            {
                var label = session.ZoneId is { } zoneId && labels.TryGetValue(zoneId, out var found) ? found : null;
                return new MyParkingSession(
                    session.Id,
                    session.LotId,
                    session.PlateNumber,
                    session.VehicleType.ToString(),
                    session.Status.ToString(),
                    session.EntryTime,
                    session.QrToken,
                    label?.ZoneCode,
                    label?.FloorName,
                    slotCodes.GetValueOrDefault(session.Id));
            })
            .ToList();
    }
}
