using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Parking;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;
using ParkingSystem.Parking.Application.ParkingLayout.ProcessSlotSensorEvent;
using ParkingSystem.Parking.Domain.ParkingLayout;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

/// <summary>
/// US-MAP-005 (BR-06.d): Test suite for IoT slot sensor event processing.
/// Verifies deduplication, clock drift handling, out-of-order event rejection,
/// tie-breaker resolution, and manual override precedence.
/// </summary>
public sealed class SlotSensorEventProcessingTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ZoneId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Happy_path_processes_sensor_occupied_event_and_enqueues_outbox_integration_event()
    {
        var slot = ParkingSlot.Create(Guid.NewGuid(), LotId, ZoneId, "A-01");
        var repository = new FakeSlotSensorRepository(slot);
        var dedupStore = new FakeSlotSensorDeduplicationStore();
        var outbox = new FakeOutboxWriter();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-10-30T10:00:00Z"));

        var handler = new ProcessSlotSensorEventCommandHandler(
            repository,
            dedupStore,
            outbox,
            clock,
            NullLogger<ProcessSlotSensorEventCommandHandler>.Instance);

        var eventId = Guid.NewGuid();
        var eventTime = DateTimeOffset.Parse("2026-10-30T09:59:58Z");

        var command = new ProcessSlotSensorEventCommand(
            eventId,
            "sensor-device-01",
            LotId,
            "A-01",
            SensorState.Occupied,
            eventTime);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Processed", result.Value.Status);
        Assert.Equal("Occupied", result.Value.EffectiveStatus);
        Assert.False(result.Value.WasOverridden);
        Assert.Equal(SensorState.Occupied, slot.SensorState);
        Assert.Equal(SlotEffectiveStatus.Occupied, slot.EffectiveStatus);

        // Verify outbox integration event
        Assert.Single(outbox.EnqueuedEvents);
        var integrationEvent = Assert.IsType<SlotStatusChangedIntegrationEvent>(outbox.EnqueuedEvents[0].Item);
        Assert.Equal(LotId, integrationEvent.LotId);
        Assert.Equal("A-01", integrationEvent.SlotCode);
        Assert.Equal("Occupied", integrationEvent.EffectiveStatus);
        Assert.Equal("Available", integrationEvent.PreviousStatus);
        Assert.Equal("Sensor", integrationEvent.Source);
    }

    [Fact]
    public async Task Duplicate_event_id_is_discarded_without_modifying_slot_or_enqueuing_event()
    {
        var slot = ParkingSlot.Create(Guid.NewGuid(), LotId, ZoneId, "A-01");
        var repository = new FakeSlotSensorRepository(slot);
        var dedupStore = new FakeSlotSensorDeduplicationStore();
        var outbox = new FakeOutboxWriter();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-10-30T10:00:00Z"));

        var handler = new ProcessSlotSensorEventCommandHandler(
            repository,
            dedupStore,
            outbox,
            clock,
            NullLogger<ProcessSlotSensorEventCommandHandler>.Instance);

        var eventId = Guid.NewGuid();
        var command = new ProcessSlotSensorEventCommand(
            eventId,
            "sensor-device-01",
            LotId,
            "A-01",
            SensorState.Occupied,
            DateTimeOffset.Parse("2026-10-30T09:59:58Z"));

        // First execution -> Processed
        var firstResult = await handler.Handle(command, TestContext.Current.CancellationToken);
        Assert.True(firstResult.IsSuccess);
        Assert.NotNull(firstResult.Value);
        Assert.Equal("Processed", firstResult.Value.Status);
        Assert.Single(outbox.EnqueuedEvents);

        // Second execution with same EventId -> DuplicateIgnored
        var secondResult = await handler.Handle(command, TestContext.Current.CancellationToken);
        Assert.True(secondResult.IsSuccess);
        Assert.NotNull(secondResult.Value);
        Assert.Equal("DuplicateIgnored", secondResult.Value.Status);
        Assert.Single(outbox.EnqueuedEvents); // No new event enqueued
    }

    [Fact]
    public async Task Clock_drift_beyond_5_seconds_is_clamped_to_received_at()
    {
        var slot = ParkingSlot.Create(Guid.NewGuid(), LotId, ZoneId, "A-01");
        var repository = new FakeSlotSensorRepository(slot);
        var dedupStore = new FakeSlotSensorDeduplicationStore();
        var outbox = new FakeOutboxWriter();
        var receivedAt = DateTimeOffset.Parse("2026-10-30T10:00:00Z");
        var clock = new FakeClock(receivedAt);

        var handler = new ProcessSlotSensorEventCommandHandler(
            repository,
            dedupStore,
            outbox,
            clock,
            NullLogger<ProcessSlotSensorEventCommandHandler>.Instance);

        // Device clock is 30 seconds into the future
        var futureEventTime = receivedAt.AddSeconds(30);

        var command = new ProcessSlotSensorEventCommand(
            Guid.NewGuid(),
            "sensor-device-01",
            LotId,
            "A-01",
            SensorState.Occupied,
            futureEventTime);

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Processed", result.Value.Status);
        // EffectiveEventTime should be clamped to receivedAt
        Assert.Equal(receivedAt, result.Value.EffectiveEventTime);
    }

    [Fact]
    public async Task Out_of_order_event_with_older_timestamp_is_discarded()
    {
        var slot = ParkingSlot.Create(Guid.NewGuid(), LotId, ZoneId, "A-01");
        var repository = new FakeSlotSensorRepository(slot);
        var dedupStore = new FakeSlotSensorDeduplicationStore();
        var outbox = new FakeOutboxWriter();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-10-30T10:00:00Z"));

        var handler = new ProcessSlotSensorEventCommandHandler(
            repository,
            dedupStore,
            outbox,
            clock,
            NullLogger<ProcessSlotSensorEventCommandHandler>.Instance);

        // Newer event arrived first (10:00:00)
        var newerCommand = new ProcessSlotSensorEventCommand(
            Guid.NewGuid(),
            "sensor-01",
            LotId,
            "A-01",
            SensorState.Occupied,
            DateTimeOffset.Parse("2026-10-30T10:00:00Z"));

        await handler.Handle(newerCommand, TestContext.Current.CancellationToken);
        Assert.Equal(SensorState.Occupied, slot.SensorState);

        // Older delayed event arrives later (09:59:00)
        var olderCommand = new ProcessSlotSensorEventCommand(
            Guid.NewGuid(),
            "sensor-01",
            LotId,
            "A-01",
            SensorState.Empty,
            DateTimeOffset.Parse("2026-10-30T09:59:00Z"));

        var olderResult = await handler.Handle(olderCommand, TestContext.Current.CancellationToken);

        Assert.True(olderResult.IsSuccess);
        Assert.NotNull(olderResult.Value);
        Assert.Equal("OutOfOrderIgnored", olderResult.Value.Status);
        // Slot state should remain Occupied from the newer event
        Assert.Equal(SensorState.Occupied, slot.SensorState);
        Assert.Equal(SlotEffectiveStatus.Occupied, slot.EffectiveStatus);
    }

    [Fact]
    public async Task Active_override_preserves_effective_status_when_sensor_updates()
    {
        // Slot is under Maintenance override
        var slot = ParkingSlot.Create(Guid.NewGuid(), LotId, ZoneId, "A-01", OverrideState.Maintenance);
        Assert.Equal(SlotEffectiveStatus.Maintenance, slot.EffectiveStatus);

        var repository = new FakeSlotSensorRepository(slot);
        var dedupStore = new FakeSlotSensorDeduplicationStore();
        var outbox = new FakeOutboxWriter();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-10-30T10:00:00Z"));

        var handler = new ProcessSlotSensorEventCommandHandler(
            repository,
            dedupStore,
            outbox,
            clock,
            NullLogger<ProcessSlotSensorEventCommandHandler>.Instance);

        var command = new ProcessSlotSensorEventCommand(
            Guid.NewGuid(),
            "sensor-01",
            LotId,
            "A-01",
            SensorState.Occupied,
            DateTimeOffset.Parse("2026-10-30T10:00:00Z"));

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Processed", result.Value.Status);
        Assert.True(result.Value.WasOverridden);
        Assert.Equal("Maintenance", result.Value.EffectiveStatus);

        // Sensor layer updated to Occupied, but effective status remains Maintenance
        Assert.Equal(SensorState.Occupied, slot.SensorState);
        Assert.Equal(SlotEffectiveStatus.Maintenance, slot.EffectiveStatus);

        // No status change event should be enqueued because effective status didn't change
        Assert.Empty(outbox.EnqueuedEvents);
    }

    [Fact]
    public async Task Nonexistent_slot_code_returns_slot_not_found_failure()
    {
        var repository = new FakeSlotSensorRepository(null);
        var dedupStore = new FakeSlotSensorDeduplicationStore();
        var outbox = new FakeOutboxWriter();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-10-30T10:00:00Z"));

        var handler = new ProcessSlotSensorEventCommandHandler(
            repository,
            dedupStore,
            outbox,
            clock,
            NullLogger<ProcessSlotSensorEventCommandHandler>.Instance);

        var command = new ProcessSlotSensorEventCommand(
            Guid.NewGuid(),
            "sensor-01",
            LotId,
            "Z-99",
            SensorState.Occupied,
            DateTimeOffset.Parse("2026-10-30T10:00:00Z"));

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("parking.slot.not_found", result.Error?.Code);
    }

    private sealed class FakeSlotSensorRepository(ParkingSlot? slot) : ISlotSensorRepository
    {
        public Task<ParkingSlot?> FindBySlotCodeAsync(Guid lotId, string slotCode, CancellationToken cancellationToken) =>
            Task.FromResult(slot);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSlotSensorDeduplicationStore : ISlotSensorDeduplicationStore
    {
        private readonly HashSet<Guid> _processed = [];
        private readonly Dictionary<(Guid, string), (DateTimeOffset, DateTimeOffset)> _lastEvents = [];

        public Task<bool> HasBeenProcessedAsync(Guid eventId, CancellationToken cancellationToken) =>
            Task.FromResult(_processed.Contains(eventId));

        public Task MarkProcessedAsync(Guid eventId, DateTimeOffset receivedAt, CancellationToken cancellationToken)
        {
            _processed.Add(eventId);
            return Task.CompletedTask;
        }

        public Task<(DateTimeOffset EventTime, DateTimeOffset ReceivedAt)?> GetLastEventInfoAsync(
            Guid lotId, string slotCode, CancellationToken cancellationToken)
        {
            return Task.FromResult(_lastEvents.TryGetValue((lotId, slotCode), out var val) ? ((DateTimeOffset, DateTimeOffset)?)val : null);
        }

        public Task RecordLastEventInfoAsync(
            Guid lotId, string slotCode, DateTimeOffset eventTime, DateTimeOffset receivedAt, CancellationToken cancellationToken)
        {
            _lastEvents[(lotId, slotCode)] = (eventTime, receivedAt);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOutboxWriter : IOutboxWriter
    {
        public List<(object Item, string Type, int Version)> EnqueuedEvents { get; } = [];

        public void Enqueue<TEvent>(
            TEvent integrationEvent,
            string eventType,
            int eventVersion,
            Guid? correlationId = null,
            Guid? causationId = null)
            where TEvent : class
        {
            EnqueuedEvents.Add((integrationEvent, eventType, eventVersion));
        }
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }
}
