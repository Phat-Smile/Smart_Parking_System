using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.GateReadings;
using ParkingSystem.Parking.Domain.GateReadings;

namespace ParkingSystem.Parking.Persistence.GateReadings;

internal sealed class GateReadingRepository(ParkingDbContext dbContext) : IGateReadingRepository
{
    public void Add(GateReading reading) => dbContext.GateReadings.Add(reading);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    public Task<GateReading?> FindByIdAsync(Guid readingId, CancellationToken cancellationToken) =>
        dbContext.GateReadings.FirstOrDefaultAsync(reading => reading.Id == readingId, cancellationToken);

    public async Task<IReadOnlyList<GateReading>> ListPendingByLotAsync(Guid lotId, CancellationToken cancellationToken) =>
        await dbContext.GateReadings.AsNoTracking()
            .Where(reading => reading.LotId == lotId && reading.Decision == GateReadingDecision.PendingStaff)
            .OrderBy(reading => reading.ReceivedAt)
            .ToListAsync(cancellationToken);

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Decision is a concurrency token: the reading was no longer PendingStaff when this save ran.
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }
}
