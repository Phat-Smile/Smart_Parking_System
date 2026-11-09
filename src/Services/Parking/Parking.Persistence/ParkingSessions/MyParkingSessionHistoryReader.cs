using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingSessions.ListMyParkingSessionHistory;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Persistence.ParkingSessions;

internal sealed class MyParkingSessionHistoryReader(ParkingDbContext dbContext) : IMyParkingSessionHistoryReader
{
    public async Task<(IReadOnlyList<ParkingSession> Items, int TotalCount)> ListAsync(
        Guid userId, ParkingSessionStatus? status, int skip, int take, CancellationToken cancellationToken)
    {
        var query = dbContext.ParkingSessions.AsNoTracking().Where(session => session.UserId == userId);
        if (status is { } wanted)
            query = query.Where(session => session.Status == wanted);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(session => session.EntryTime)
            .ThenBy(session => session.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }
}
