using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.Incidents;
using ParkingSystem.Parking.Domain.Incidents;

namespace ParkingSystem.Parking.Persistence.Incidents;

internal sealed class IncidentRepository(ParkingDbContext dbContext) : IIncidentRepository
{
    public void Add(Incident incident) => dbContext.Incidents.Add(incident);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    public Task<Incident?> FindByIdAsync(Guid incidentId, CancellationToken cancellationToken) =>
        dbContext.Incidents.FirstOrDefaultAsync(incident => incident.Id == incidentId, cancellationToken);

    public async Task<Incident?> FindByIdForUpdateAsync(Guid incidentId, Guid lotId, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Gate reading requires a Parking transaction.");

        var rows = await dbContext.Incidents
            .FromSqlInterpolated($"SELECT * FROM parking.incidents WHERE id = {incidentId} AND lot_id = {lotId} FOR UPDATE")
            .AsTracking()
            .ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    public async Task<IReadOnlyList<Incident>> ListOpenByLotAsync(Guid lotId, IncidentType incidentType, CancellationToken cancellationToken) =>
        await dbContext.Incidents.AsNoTracking()
            .Where(incident => incident.LotId == lotId
                && incident.Status == IncidentStatus.Open
                && incident.IncidentType == incidentType)
            .OrderBy(incident => incident.OccurredAt)
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
            // Status is a concurrency token: the incident was no longer Open when this save ran.
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }
}
