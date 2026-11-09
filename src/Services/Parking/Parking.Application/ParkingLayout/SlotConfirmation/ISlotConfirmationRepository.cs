using ParkingSystem.Parking.Domain.Incidents;
using ParkingSystem.Parking.Domain.ParkingLayout;

namespace ParkingSystem.Parking.Application.ParkingLayout.SlotConfirmation;

public interface ISlotConfirmationRepository
{
    /// <summary>The tracked slot and whether its zone has slot sensors, or null when the slot does not exist.</summary>
    Task<(ParkingSlot Slot, bool ZoneHasSlotSensor)?> FindWithZoneAsync(Guid slotId, CancellationToken cancellationToken);

    /// <summary>Tracked Assigned slots given out before the cutoff, oldest first.</summary>
    Task<IReadOnlyList<ParkingSlot>> ListAssignedBeforeAsync(DateTimeOffset cutoff, int take, CancellationToken cancellationToken);

    /// <summary>Tracked NeedsVerification slots older than the cutoff whose Owner was not told yet, oldest first.</summary>
    Task<IReadOnlyList<ParkingSlot>> ListOwnerNotNotifiedBeforeAsync(DateTimeOffset cutoff, int take, CancellationToken cancellationToken);

    /// <summary>The tracked open ParkConfirmTimeout incident of the slot, or null.</summary>
    Task<Incident?> FindOpenTimeoutIncidentAsync(Guid slotId, CancellationToken cancellationToken);

    void AddIncident(Incident incident);

    /// <summary>Saves pending changes. Returns false when the slot changed since it was read (row_version).</summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
