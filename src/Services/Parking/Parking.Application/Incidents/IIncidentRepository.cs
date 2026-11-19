using ParkingSystem.Parking.Domain.Incidents;

namespace ParkingSystem.Parking.Application.Incidents;

public interface IIncidentRepository
{
    void Add(Incident incident);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>The tracked incident, or null when it does not exist.</summary>
    Task<Incident?> FindByIdAsync(Guid incidentId, CancellationToken cancellationToken);

    /// <summary>Locks the incident in the requested lot until the caller's Parking transaction ends.</summary>
    Task<Incident?> FindByIdForUpdateAsync(Guid incidentId, Guid lotId, CancellationToken cancellationToken);

    /// <summary>Open incidents of one type for the lot, oldest first (read only).</summary>
    Task<IReadOnlyList<Incident>> ListOpenByLotAsync(Guid lotId, IncidentType incidentType, CancellationToken cancellationToken);

    /// <summary>Saves pending changes. Returns false when another staff member resolved the same incident first.</summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
