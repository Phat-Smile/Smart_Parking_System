using ParkingSystem.Parking.Application.ParkingLayout;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

// Contract-first stub (master sheet, contracts tab): returns a configured number, so Booking can test
// "lot has room" and "lot is full" before lot owners can declare slots.
internal sealed class StubAvailableSlotReader(AvailabilityOptions options) : IAvailableSlotReader
{
    public Task<int> CountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(options.StubAvailableSlotCount);
    }
}
