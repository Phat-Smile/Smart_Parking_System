using System.Collections.Concurrent;
using ParkingSystem.Parking.Application.ParkingLayout.ProcessSlotSensorEvent;
using ParkingSystem.Parking.Domain.ParkingLayout;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

/// <summary>
/// Thread-safe in-memory deduplication store for IoT sensor events (BR-06.d).
/// Tracks processed event IDs with a 7-day retention window,
/// and maintains the latest event timeline per (LotId, SlotCode).
/// </summary>
internal sealed class SlotSensorDeduplicationStore : ISlotSensorDeduplicationStore
{
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(7);

    // Key: EventId -> Value: ReceivedAt
    private static readonly ConcurrentDictionary<Guid, DateTimeOffset> ProcessedEvents = new();

    // Key: (LotId, NormalizedSlotCode) -> Value: (EventTime, ReceivedAt)
    private static readonly ConcurrentDictionary<(Guid LotId, string SlotCode), (DateTimeOffset EventTime, DateTimeOffset ReceivedAt)> SlotLatestEvent = new();

    public Task<bool> HasBeenProcessedAsync(Guid eventId, CancellationToken cancellationToken)
    {
        return Task.FromResult(ProcessedEvents.ContainsKey(eventId));
    }

    public Task MarkProcessedAsync(Guid eventId, DateTimeOffset receivedAt, CancellationToken cancellationToken)
    {
        ProcessedEvents[eventId] = receivedAt;

        // Periodic light cleanup if collection gets large
        if (ProcessedEvents.Count > 10_000)
        {
            var cutoff = receivedAt - RetentionPeriod;
            foreach (var kvp in ProcessedEvents)
            {
                if (kvp.Value < cutoff)
                {
                    ProcessedEvents.TryRemove(kvp.Key, out _);
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task<(DateTimeOffset EventTime, DateTimeOffset ReceivedAt)?> GetLastEventInfoAsync(
        Guid lotId,
        string slotCode,
        CancellationToken cancellationToken)
    {
        var key = (lotId, ParkingSlot.NormalizeSlotCode(slotCode));
        if (SlotLatestEvent.TryGetValue(key, out var info))
        {
            return Task.FromResult<(DateTimeOffset EventTime, DateTimeOffset ReceivedAt)?>(info);
        }

        return Task.FromResult<(DateTimeOffset EventTime, DateTimeOffset ReceivedAt)?>(null);
    }

    public Task RecordLastEventInfoAsync(
        Guid lotId,
        string slotCode,
        DateTimeOffset eventTime,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken)
    {
        var key = (lotId, ParkingSlot.NormalizeSlotCode(slotCode));
        SlotLatestEvent.AddOrUpdate(
            key,
            (eventTime, receivedAt),
            (_, old) => eventTime >= old.EventTime ? (eventTime, receivedAt) : old);

        return Task.CompletedTask;
    }
}
