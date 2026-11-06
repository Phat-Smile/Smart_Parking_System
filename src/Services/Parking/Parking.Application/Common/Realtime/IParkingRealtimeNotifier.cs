namespace ParkingSystem.Parking.Application.Common.Realtime;

public interface IParkingRealtimeNotifier
{
    Task NotifySlotStatusChangedAsync(
        Guid lotId,
        Guid slotId,
        string slotCode,
        string effectiveStatus,
        string previousStatus,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default);

    // US-PARK-005 (BR-04.e): stage is Staff (10 minutes without a confirmation) or Owner (30 minutes in NeedsVerification).
    Task NotifySlotNeedsVerificationAsync(
        Guid lotId,
        Guid slotId,
        string slotCode,
        string stage,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default);

    Task NotifyLotOccupancyChangedAsync(
        Guid lotId,
        int availableCarSlots,
        int availableMotorbikeSlots,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default);
}
