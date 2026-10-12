namespace ParkingSystem.Parking.Application.ParkingLayout;

public interface IAvailableSlotReader
{
    /// <summary>BR-02.a: number of car slots whose effective status is Available.</summary>
    Task<int> CountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken);
}
