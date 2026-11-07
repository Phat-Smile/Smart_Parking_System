using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Application.ParkingLayout.SlotConfirmation;
using ParkingSystem.Parking.Domain.Incidents;
using ParkingSystem.Parking.Domain.ParkingLayout;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

internal sealed class SlotConfirmationRepository(ParkingDbContext dbContext) : ISlotConfirmationRepository
{
    public async Task<(ParkingSlot Slot, bool ZoneHasSlotSensor)?> FindWithZoneAsync(Guid slotId, CancellationToken cancellationToken)
    {
        var slot = await dbContext.ParkingSlots.FirstOrDefaultAsync(item => item.Id == slotId, cancellationToken);
        if (slot is null)
            return null;

        var hasSensor = await dbContext.Zones.AsNoTracking()
            .Where(zone => zone.Id == slot.ZoneId)
            .Select(zone => zone.HasSlotSensor)
            .FirstAsync(cancellationToken);
        return (slot, hasSensor);
    }

    public async Task<IReadOnlyList<ParkingSlot>> ListAssignedBeforeAsync(DateTimeOffset cutoff, int take, CancellationToken cancellationToken) =>
        await dbContext.ParkingSlots
            .Where(slot => slot.DispatchState == DispatchState.Assigned && slot.AssignedAt < cutoff)
            .OrderBy(slot => slot.AssignedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ParkingSlot>> ListOwnerNotNotifiedBeforeAsync(DateTimeOffset cutoff, int take, CancellationToken cancellationToken) =>
        await dbContext.ParkingSlots
            .Where(slot => slot.DispatchState == DispatchState.NeedsVerification
                && slot.NeedsVerificationSince < cutoff
                && slot.OwnerNotifiedAt == null)
            .OrderBy(slot => slot.NeedsVerificationSince)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<Incident?> FindOpenTimeoutIncidentAsync(Guid slotId, CancellationToken cancellationToken) =>
        dbContext.Incidents.FirstOrDefaultAsync(
            incident => incident.SlotId == slotId
                && incident.IncidentType == IncidentType.ParkConfirmTimeout
                && incident.Status == IncidentStatus.Open,
            cancellationToken);

    public void AddIncident(Incident incident) => dbContext.Incidents.Add(incident);

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // row_version (or the incident status) is a concurrency token: someone changed it first.
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }
}
