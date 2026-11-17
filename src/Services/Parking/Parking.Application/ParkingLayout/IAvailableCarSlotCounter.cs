namespace ParkingSystem.Parking.Application.ParkingLayout;

/// <summary>
/// US-PARK-004 (BR-02.f, point 35 option a): the real number of Available car slots of the lot, counted without the
/// car-entry lock. The gate asks it before checking a reservation in; it is never replaced by the development stub of
/// contract #2, because the gate decision must match the slots the designator will look at.
/// </summary>
public interface IAvailableCarSlotCounter
{
    Task<int> CountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken);
}
