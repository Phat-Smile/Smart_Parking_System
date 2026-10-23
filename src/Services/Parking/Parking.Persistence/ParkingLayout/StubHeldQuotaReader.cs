using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

// Contract-first stub for Booking contract #4 (held-quota count, BE-3). Returns 0 until Booking provides it,
// so the BR-02.f warning never fires yet.
internal sealed class StubHeldQuotaReader : IHeldQuotaReader
{
    public Task<int> CountHeldReservationsAsync(Guid lotId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(0);
    }
}
