using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Persistence.ParkingSessions;

internal sealed class ParkingSessionRepository(ParkingDbContext dbContext) : IParkingSessionRepository
{
    public Task<bool> HasOpenSessionAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) =>
        dbContext.ParkingSessions.AsNoTracking().AnyAsync(
            session => session.LotId == lotId
                       && session.PlateNumber == normalizedPlate
                       && ParkingSession.OpenStatuses.Contains(session.Status),
            cancellationToken);

    public void Add(ParkingSession session) => dbContext.ParkingSessions.Add(session);

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ParkingSessionConfiguration.OpenPlateIndexName
        })
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }
}
