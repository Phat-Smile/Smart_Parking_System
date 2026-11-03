using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Domain.ParkingLayout;

namespace ParkingSystem.Parking.Application.ParkingLayout.ProcessSlotSensorEvent;

/// <summary>
/// US-MAP-005 (BR-06.d): Xử lý sự kiện cảm biến IoT tại từng vị trí đỗ.
/// Hỗ trợ khử trùng (deduplication), xử lý lệch đồng hồ (clock drift),
/// và xử lý sự kiện đến trễ (out-of-order).
/// </summary>
public sealed record ProcessSlotSensorEventCommand(
    Guid EventId,
    string DeviceId,
    Guid LotId,
    string SlotCode,
    SensorState State,
    DateTimeOffset EventTime
) : IRequest<Result<ProcessedSensorEventResponse>>;

public sealed record ProcessedSensorEventResponse(
    Guid EventId,
    Guid LotId,
    string SlotCode,
    string Status, // "Processed", "DuplicateIgnored", "OutOfOrderIgnored"
    string EffectiveStatus,
    bool WasOverridden,
    DateTimeOffset EffectiveEventTime,
    DateTimeOffset ReceivedAt
);

public static class ProcessSlotSensorEventErrors
{
    public static readonly Error SlotNotFound =
        new("parking.slot.not_found", "The specified parking slot was not found in the parking lot.");

    public static readonly Error InvalidSensorState =
        new("parking.slot.invalid_sensor_state", "SensorState must be either 'Empty' or 'Occupied'.");
}
