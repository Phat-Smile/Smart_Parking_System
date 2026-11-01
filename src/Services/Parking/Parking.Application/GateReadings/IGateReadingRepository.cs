using ParkingSystem.Parking.Domain.GateReadings;

namespace ParkingSystem.Parking.Application.GateReadings;

public interface IGateReadingRepository
{
    void Add(GateReading reading);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>The tracked reading, or null when it does not exist.</summary>
    Task<GateReading?> FindByIdAsync(Guid readingId, CancellationToken cancellationToken);

    /// <summary>Readings of the lot waiting for staff, oldest first (read only).</summary>
    Task<IReadOnlyList<GateReading>> ListPendingByLotAsync(Guid lotId, CancellationToken cancellationToken);

    /// <summary>Saves pending changes. Returns false when another staff member confirmed the same reading first.</summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
