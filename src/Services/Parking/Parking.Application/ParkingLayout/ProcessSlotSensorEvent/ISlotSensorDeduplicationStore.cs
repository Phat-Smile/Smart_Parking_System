namespace ParkingSystem.Parking.Application.ParkingLayout.ProcessSlotSensorEvent;

/// <summary>
/// Quản lý khử trùng sự kiện cảm biến (BR-06.d):
/// - Khử trùng theo event_id trong vòng 7 ngày.
/// - Lưu vết mốc thời gian sự kiện mới nhất theo từng slot để xử lý sự kiện đến trễ (out-of-order).
/// </summary>
public interface ISlotSensorDeduplicationStore
{
    Task<bool> HasBeenProcessedAsync(Guid eventId, CancellationToken cancellationToken);
    Task MarkProcessedAsync(Guid eventId, DateTimeOffset receivedAt, CancellationToken cancellationToken);
    Task<(DateTimeOffset EventTime, DateTimeOffset ReceivedAt)?> GetLastEventInfoAsync(Guid lotId, string slotCode, CancellationToken cancellationToken);
    Task RecordLastEventInfoAsync(Guid lotId, string slotCode, DateTimeOffset eventTime, DateTimeOffset receivedAt, CancellationToken cancellationToken);
}
