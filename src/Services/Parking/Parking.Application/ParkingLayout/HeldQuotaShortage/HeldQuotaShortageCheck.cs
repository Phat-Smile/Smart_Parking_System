namespace ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;

/// <summary>
/// Booking contract #4 (BE-3): reservations held for a lot right now (BR-02.a, BR-02.e).
/// Stubbed until Booking provides it.
/// </summary>
public interface IHeldQuotaReader
{
    Task<int> CountHeldReservationsAsync(Guid lotId, CancellationToken cancellationToken);
}

public sealed record CapacityWarning(string Code, string Message, int AvailableSlots, int HeldQuota);

/// <summary>
/// BR-02.f: warn the owner (never block) when Available car slots drop below the reservations held.
/// One shared check: US-OWNER-003 returns it in the response; US-STAFF-001/002 must call this same check and
/// send a Notification to the owner instead of writing a second one.
/// </summary>
public interface IHeldQuotaShortageCheck
{
    Task<CapacityWarning?> CheckAsync(Guid lotId, CancellationToken cancellationToken);
}

public sealed class HeldQuotaShortageCheck(IAvailableSlotReader availableSlots, IHeldQuotaReader heldQuota)
    : IHeldQuotaShortageCheck
{
    public const string WarningCode = "parking.capacity.below_held_quota";

    public async Task<CapacityWarning?> CheckAsync(Guid lotId, CancellationToken cancellationToken)
    {
        var held = await heldQuota.CountHeldReservationsAsync(lotId, cancellationToken);
        if (held == 0)
            return null;

        var available = await availableSlots.CountAvailableCarSlotsAsync(lotId, cancellationToken);
        return available >= held
            ? null
            : new CapacityWarning(
                WarningCode,
                $"Available car slots ({available}) are lower than the reservations held ({held}).",
                available,
                held);
    }
}
