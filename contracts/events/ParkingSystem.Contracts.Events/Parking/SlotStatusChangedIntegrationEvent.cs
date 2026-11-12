namespace ParkingSystem.Contracts.Events.Parking;

/// <summary>
/// Hợp đồng sự kiện #7: slot.status.changed
/// Được phát ra khi trạng thái hiệu lực của một vị trí đỗ (Car Parking Slot) thay đổi.
/// Consumer: SignalR Hub (đẩy realtime xuống FE 3D/Dashboard) và AI Recommendation Engine.
/// </summary>
public record SlotStatusChangedIntegrationEvent(
    Guid EventId,
    Guid LotId,
    Guid ZoneId,
    Guid SlotId,
    string SlotCode,
    string EffectiveStatus,
    string? PreviousStatus,
    string Source,
    DateTimeOffset OccurredAtUtc,
    int Version
);
