using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Parking;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;
using ParkingSystem.Parking.Application.Common.Realtime;
using ParkingSystem.Parking.Domain.ParkingLayout;

namespace ParkingSystem.Parking.Application.ParkingLayout.ProcessSlotSensorEvent;

public sealed class ProcessSlotSensorEventCommandHandler(
    ISlotSensorRepository repository,
    ISlotSensorDeduplicationStore dedupStore,
    IOutboxWriter outboxWriter,
    IClock clock,
    ILogger<ProcessSlotSensorEventCommandHandler> logger,
    IParkingRealtimeNotifier? realtimeNotifier = null)
    : IRequestHandler<ProcessSlotSensorEventCommand, Result<ProcessedSensorEventResponse>>
{
    public const int DefaultClockDriftToleranceSeconds = 5;

    public async Task<Result<ProcessedSensorEventResponse>> Handle(
        ProcessSlotSensorEventCommand request,
        CancellationToken cancellationToken)
    {
        var receivedAt = clock.UtcNow;

        // 1. Chống trùng lặp (Deduplication theo event_id, BR-06.d bước 2)
        if (await dedupStore.HasBeenProcessedAsync(request.EventId, cancellationToken))
        {
            logger.LogInformation(
                "Duplicate sensor event discarded. EventId={EventId}, LotId={LotId}, SlotCode={SlotCode}",
                request.EventId, request.LotId, request.SlotCode);

            return Result<ProcessedSensorEventResponse>.Success(new ProcessedSensorEventResponse(
                request.EventId,
                request.LotId,
                request.SlotCode,
                "DuplicateIgnored",
                "Ignored",
                WasOverridden: false,
                request.EventTime,
                receivedAt));
        }

        // 2. Xử lý lệch đồng hồ (Clock drift, BR-06.d bước 5)
        var effectiveEventTime = request.EventTime;
        if (effectiveEventTime > receivedAt.AddSeconds(DefaultClockDriftToleranceSeconds))
        {
            logger.LogWarning(
                "Clock drift detected on device {DeviceId}. EventTime={EventTime} exceeds receivedAt={ReceivedAt} by >5s. Clamping to receivedAt.",
                request.DeviceId, request.EventTime, receivedAt);
            effectiveEventTime = receivedAt;
        }

        // 3. Tra cứu vị trí đỗ (ParkingSlot)
        var slot = await repository.FindBySlotCodeAsync(request.LotId, request.SlotCode, cancellationToken);
        if (slot is null)
        {
            logger.LogWarning(
                "Parking slot not found for sensor event. LotId={LotId}, SlotCode={SlotCode}",
                request.LotId, request.SlotCode);
            return Result<ProcessedSensorEventResponse>.Failure(ProcessSlotSensorEventErrors.SlotNotFound);
        }

        // 4. Xử lý sự kiện đến trễ (Out-of-order, BR-06.d bước 3 & 4)
        var lastEventInfo = await dedupStore.GetLastEventInfoAsync(request.LotId, slot.SlotCode, cancellationToken);
        if (lastEventInfo is not null)
        {
            var (lastEventTime, _) = lastEventInfo.Value;

            if (effectiveEventTime < lastEventTime)
            {
                logger.LogInformation(
                    "Out-of-order sensor event discarded. EventTime={EventTime} is older than LastEventTime={LastEventTime} for Slot={SlotCode}",
                    effectiveEventTime, lastEventTime, slot.SlotCode);

                return Result<ProcessedSensorEventResponse>.Success(new ProcessedSensorEventResponse(
                    request.EventId,
                    request.LotId,
                    slot.SlotCode,
                    "OutOfOrderIgnored",
                    slot.EffectiveStatus.ToString(),
                    WasOverridden: false,
                    effectiveEventTime,
                    receivedAt));
            }

            // Device timestamps can be equal even when packets arrive in the reverse order.
            // Arrival order is not proof of occupancy order: never flip a slot on an ambiguous tie.
            if (effectiveEventTime == lastEventTime)
            {
                logger.LogInformation(
                    "Concurrent/older sensor event tie-breaker discarded for Slot={SlotCode}",
                    slot.SlotCode);

                return Result<ProcessedSensorEventResponse>.Success(new ProcessedSensorEventResponse(
                    request.EventId,
                    request.LotId,
                    slot.SlotCode,
                    "OutOfOrderIgnored",
                    slot.EffectiveStatus.ToString(),
                    WasOverridden: false,
                    effectiveEventTime,
                    receivedAt));
            }
        }

        // 5. Cập nhật trạng thái cảm biến cho Slot (BR-06.d bước 6 & BR-06.a)
        var previousStatus = slot.EffectiveStatus;
        slot.UpdateSensorState(request.State);

        var wasOverridden = slot.OverrideState is OverrideState.Maintenance or OverrideState.Unavailable;
        if (wasOverridden)
        {
            logger.LogInformation(
                "Sensor state updated to {SensorState} for Slot={SlotCode}, but effective status remains {EffectiveStatus} due to active Override ({OverrideState}).",
                request.State, slot.SlotCode, slot.EffectiveStatus, slot.OverrideState);
        }

        // 6. Phát sự kiện tích hợp slot.status.changed nếu trạng thái hiệu lực thay đổi
        if (slot.EffectiveStatus != previousStatus)
        {
            outboxWriter.Enqueue(
                new SlotStatusChangedIntegrationEvent(
                    Guid.NewGuid(),
                    slot.LotId,
                    slot.ZoneId,
                    slot.Id,
                    slot.SlotCode,
                    slot.EffectiveStatus.ToString(),
                    previousStatus.ToString(),
                    "Sensor",
                    effectiveEventTime,
                    slot.RowVersion),
                "parking.slot-status-changed.v1",
                1);

        }

        // 7. Lưu thay đổi và cập nhật mốc thời gian khử trùng
        await repository.SaveChangesAsync(cancellationToken);
        await dedupStore.MarkProcessedAsync(request.EventId, receivedAt, cancellationToken);
        await dedupStore.RecordLastEventInfoAsync(request.LotId, slot.SlotCode, effectiveEventTime, receivedAt, cancellationToken);

        // Broadcast only after persistence succeeds, never expose an uncommitted slot state.
        if (slot.EffectiveStatus != previousStatus && realtimeNotifier is not null)
        {
            try
            {
                await realtimeNotifier.NotifySlotStatusChangedAsync(
                    slot.LotId, slot.Id, slot.SlotCode, slot.EffectiveStatus.ToString(),
                    previousStatus.ToString(), effectiveEventTime, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // The DB and outbox are already saved. Returning a failure causes device retries
                // that hit dedup but cannot replay this SignalR notification. The persisted
                // slot-status-changed outbox event remains the reliable integration record.
                logger.LogWarning(exception,
                    "Realtime slot notification failed after persistence. EventId={EventId}, LotId={LotId}, SlotCode={SlotCode}",
                    request.EventId, slot.LotId, slot.SlotCode);
            }
        }

        return Result<ProcessedSensorEventResponse>.Success(new ProcessedSensorEventResponse(
            request.EventId,
            request.LotId,
            slot.SlotCode,
            "Processed",
            slot.EffectiveStatus.ToString(),
            wasOverridden,
            effectiveEventTime,
            receivedAt));
    }
}
