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

    Task NotifyLotOccupancyChangedAsync(
        Guid lotId,
        int availableCarSlots,
        int availableMotorbikeSlots,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default);
}
