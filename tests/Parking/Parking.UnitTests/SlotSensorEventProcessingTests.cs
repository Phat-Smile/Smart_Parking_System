using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Parking;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;
using ParkingSystem.Parking.Application.Common.Realtime;
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

    [Fact]
    public async Task Sensor_event_broadcasts_realtime_status_when_effective_status_changes()
    {
        var slot = ParkingSlot.Create(Guid.NewGuid(), LotId, ZoneId, "A-01");
        var repository = new FakeSlotSensorRepository(slot);
        var dedupStore = new FakeSlotSensorDeduplicationStore();
        var outbox = new FakeOutboxWriter();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-10-30T10:00:00Z"));
        var realtime = new FakeParkingRealtimeNotifier();

        var handler = new ProcessSlotSensorEventCommandHandler(
            repository,
            dedupStore,
            outbox,
            clock,
            NullLogger<ProcessSlotSensorEventCommandHandler>.Instance,
            realtime);

        var command = new ProcessSlotSensorEventCommand(
            Guid.NewGuid(),
            "sensor-01",
            LotId,
            "A-01",
            SensorState.Occupied,
            DateTimeOffset.Parse("2026-10-30T09:59:58Z"));

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var broadcast = Assert.Single(realtime.SlotChanges);
        Assert.Equal(LotId, broadcast.LotId);
        Assert.Equal("A-01", broadcast.SlotCode);
        Assert.Equal("Occupied", broadcast.EffectiveStatus);
        Assert.Equal("Available", broadcast.PreviousStatus);
    }

    [Fact]
    public async Task Sensor_event_does_not_broadcast_when_status_is_unchanged()
    {
        var slot = ParkingSlot.Create(Guid.NewGuid(), LotId, ZoneId, "A-01");
        // Initial sensor state is already Empty -> sending Empty won't change effective status
        var repository = new FakeSlotSensorRepository(slot);
        var dedupStore = new FakeSlotSensorDeduplicationStore();
        var outbox = new FakeOutboxWriter();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-10-30T10:00:00Z"));
        var realtime = new FakeParkingRealtimeNotifier();

        var handler = new ProcessSlotSensorEventCommandHandler(
            repository,
            dedupStore,
            outbox,
            clock,
            NullLogger<ProcessSlotSensorEventCommandHandler>.Instance,
            realtime);

        var command = new ProcessSlotSensorEventCommand(
            Guid.NewGuid(),
            "sensor-01",
            LotId,
            "A-01",
            SensorState.Empty,
            DateTimeOffset.Parse("2026-10-30T09:59:58Z"));

        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(realtime.SlotChanges);
    }

    [Fact]
    public void Validator_rejects_missing_device_event_time()
    {
        var validator = new ProcessSlotSensorEventCommandValidator();
        var valid = new ProcessSlotSensorEventCommand(
            Guid.NewGuid(), "sensor-1", LotId, "A-01", SensorState.Empty,
            DateTimeOffset.Parse("2026-11-20T03:00:00Z"));

        Assert.True(validator.Validate(valid).IsValid);
        Assert.False(validator.Validate(valid with { EventTime = default }).IsValid);
        Assert.False(validator.Validate(valid with { EventId = Guid.Empty }).IsValid);
    }

    [Fact]
    public async Task Failed_persistence_must_not_broadcast_uncommitted_sensor_status()
    {
        var slot = ParkingSlot.Create(Guid.NewGuid(), LotId, ZoneId, "A-01");
        var repository = new FakeSlotSensorRepository(slot) { FailSave = true };
        var dedupStore = new FakeSlotSensorDeduplicationStore();
        var notifier = new FakeParkingRealtimeNotifier();
        var handler = new ProcessSlotSensorEventCommandHandler(
            repository, dedupStore, new FakeOutboxWriter(),
            new FakeClock(DateTimeOffset.Parse("2026-11-20T03:00:00Z")),
            NullLogger<ProcessSlotSensorEventCommandHandler>.Instance, notifier);
        var eventId = Guid.NewGuid();
        var command = new ProcessSlotSensorEventCommand(
            eventId, "sensor-1", LotId, "A-01", SensorState.Occupied,
            DateTimeOffset.Parse("2026-11-20T02:59:59Z"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Empty(notifier.SlotChanges);
        Assert.False(await dedupStore.HasBeenProcessedAsync(eventId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Equal_timestamp_from_a_different_event_cannot_revert_the_latest_slot_state()
    {
        var slot = ParkingSlot.Create(Guid.NewGuid(), LotId, ZoneId, "A-01");
        var dedup = new FakeSlotSensorDeduplicationStore();
        var outbox = new FakeOutboxWriter();
        var handler = new ProcessSlotSensorEventCommandHandler(
            new FakeSlotSensorRepository(slot), dedup, outbox,
            new FakeClock(DateTimeOffset.Parse("2026-11-20T10:00:00Z")),
            NullLogger<ProcessSlotSensorEventCommandHandler>.Instance);
        var timestamp = DateTimeOffset.Parse("2026-11-20T09:59:58Z");
        var first = await handler.Handle(new ProcessSlotSensorEventCommand(
            Guid.NewGuid(), "device-1", LotId, "A-01", SensorState.Occupied, timestamp),
            TestContext.Current.CancellationToken);
        var second = await handler.Handle(new ProcessSlotSensorEventCommand(
            Guid.NewGuid(), "device-2", LotId, "A-01", SensorState.Empty, timestamp),
            TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("OutOfOrderIgnored", second.Value?.Status);
        Assert.Equal(SlotEffectiveStatus.Occupied, slot.EffectiveStatus);
        Assert.Single(outbox.EnqueuedEvents);
    }

    [Fact]
    public async Task Failure_to_broadcast_after_successful_save_does_not_retry_a_committed_sensor_event()
    {
        var slot = ParkingSlot.Create(Guid.NewGuid(), LotId, ZoneId, "A-01");
        var dedup = new FakeSlotSensorDeduplicationStore();
        var repository = new FakeSlotSensorRepository(slot);
        var outbox = new FakeOutboxWriter();
        var eventId = Guid.NewGuid();
        var notifier = new ThrowingParkingRealtimeNotifier();
        var handler = new ProcessSlotSensorEventCommandHandler(
            repository, dedup, outbox,
            new FakeClock(DateTimeOffset.Parse("2026-11-20T10:00:00Z")),
            NullLogger<ProcessSlotSensorEventCommandHandler>.Instance,
            notifier);
        var command = new ProcessSlotSensorEventCommand(
            eventId, "device-1", LotId, "A-01", SensorState.Occupied,
            DateTimeOffset.Parse("2026-11-20T09:59:58Z"));
        var first = await handler.Handle(command, TestContext.Current.CancellationToken);
        var again = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.Equal("Processed", first.Value?.Status);
        Assert.Equal("DuplicateIgnored", again.Value?.Status);
        Assert.True(await dedup.HasBeenProcessedAsync(eventId, TestContext.Current.CancellationToken));
        Assert.Single(outbox.EnqueuedEvents);
        Assert.Equal(1, notifier.Attempts);
    }

    private sealed class ThrowingParkingRealtimeNotifier : IParkingRealtimeNotifier
    {
        public int Attempts { get; private set; }

        public Task NotifySlotStatusChangedAsync(Guid lotId, Guid slotId, string slotCode,
            string effectiveStatus, string previousStatus, DateTimeOffset timestamp,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            throw new InvalidOperationException("Simulated SignalR outage");
        }

        public Task NotifySlotNeedsVerificationAsync(Guid lotId, Guid slotId, string slotCode,
            string stage, DateTimeOffset timestamp, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifyLotOccupancyChangedAsync(Guid lotId, int availableCarSlots, int availableMotorbikeSlots,
            DateTimeOffset timestamp, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSlotSensorRepository(ParkingSlot? slot) : ISlotSensorRepository
    {
        public bool FailSave { get; init; }
        public Task<ParkingSlot?> FindBySlotCodeAsync(Guid lotId, string slotCode, CancellationToken cancellationToken) =>
            Task.FromResult(slot);

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            FailSave ? Task.FromException(new InvalidOperationException("Simulated DB failure")) : Task.CompletedTask;
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

    private sealed class FakeParkingRealtimeNotifier : IParkingRealtimeNotifier
    {
        public Task NotifySlotNeedsVerificationAsync(
            Guid lotId, Guid slotId, string slotCode, string stage, DateTimeOffset timestamp, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public List<(Guid LotId, Guid SlotId, string SlotCode, string EffectiveStatus, string PreviousStatus, DateTimeOffset Timestamp)> SlotChanges { get; } = [];
        public List<(Guid LotId, int AvailableCarSlots, int AvailableMotorbikeSlots, DateTimeOffset Timestamp)> OccupancyChanges { get; } = [];

        public Task NotifySlotStatusChangedAsync(
            Guid lotId,
            Guid slotId,
            string slotCode,
            string effectiveStatus,
            string previousStatus,
            DateTimeOffset timestamp,
            CancellationToken cancellationToken = default)
        {
            SlotChanges.Add((lotId, slotId, slotCode, effectiveStatus, previousStatus, timestamp));
            return Task.CompletedTask;
        }

        public Task NotifyLotOccupancyChangedAsync(
            Guid lotId,
            int availableCarSlots,
            int availableMotorbikeSlots,
            DateTimeOffset timestamp,
            CancellationToken cancellationToken = default)
        {
            OccupancyChanges.Add((lotId, availableCarSlots, availableMotorbikeSlots, timestamp));
            return Task.CompletedTask;
        }
    }
}
