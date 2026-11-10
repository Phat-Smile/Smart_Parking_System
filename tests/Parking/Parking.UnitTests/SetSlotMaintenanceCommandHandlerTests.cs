using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Parking;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;
using ParkingSystem.Parking.Application.Common.Realtime;
using ParkingSystem.Parking.Application.ParkingLayout.SetSlotMaintenance;
using ParkingSystem.Parking.Domain.ParkingLayout;
using Xunit;

namespace ParkingSystem.Parking.UnitTests;

public class SetSlotMaintenanceCommandHandlerTests
{
    private sealed class FakeSlotMaintenanceRepository : ISlotMaintenanceRepository
    {
        public bool CanManage { get; set; } = true;
        public ParkingSlot? SlotToReturn { get; set; }
        public int AvailableCarSlotsToReturn { get; set; } = 15;
        public bool SaveChangesCalled { get; private set; }

        public Task<bool> CanManageLotAsync(Guid lotId, Guid callerAccountId, string callerRole, CancellationToken cancellationToken)
        {
            if (string.Equals(callerRole, "Admin", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(true);

            return Task.FromResult(CanManage);
        }

        public Task<ParkingSlot?> FindSlotAsync(Guid lotId, Guid slotId, CancellationToken cancellationToken)
            => Task.FromResult(SlotToReturn);

        public Task<int> GetAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken)
            => Task.FromResult(AvailableCarSlotsToReturn);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOutboxWriter : IOutboxWriter
    {
        public readonly List<object> EnqueuedEvents = [];

        public void Enqueue<TEvent>(
            TEvent integrationEvent, string eventType, int eventVersion,
            Guid? correlationId = null, Guid? causationId = null)
            where TEvent : class
        {
            EnqueuedEvents.Add(integrationEvent);
        }
    }

    private sealed class FakeRealtimeNotifier : IParkingRealtimeNotifier
    {
        public readonly List<(Guid LotId, Guid SlotId, string EffectiveStatus)> SlotChanges = [];
        public readonly List<(Guid LotId, int AvailableCarSlots)> OccupancyChanges = [];

        public Task NotifySlotStatusChangedAsync(
            Guid lotId, Guid slotId, string slotCode, string effectiveStatus, string previousStatus,
            DateTimeOffset timestamp, CancellationToken cancellationToken = default)
        {
            SlotChanges.Add((lotId, slotId, effectiveStatus));
            return Task.CompletedTask;
        }

        public Task NotifySlotNeedsVerificationAsync(
            Guid lotId, Guid slotId, string slotCode, string stage, DateTimeOffset timestamp,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifyLotOccupancyChangedAsync(
            Guid lotId, int availableCarSlots, int availableMotorbikeSlots, DateTimeOffset timestamp,
            CancellationToken cancellationToken = default)
        {
            OccupancyChanges.Add((lotId, availableCarSlots));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    private readonly FakeSlotMaintenanceRepository _repo = new();
    private readonly FakeOutboxWriter _outbox = new();
    private readonly FakeRealtimeNotifier _notifier = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.Zero));
    private readonly Guid _lotId = Guid.NewGuid();
    private readonly Guid _slotId = Guid.NewGuid();
    private readonly Guid _zoneId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    [Fact]
    public async Task Handle_UnauthorizedCaller_ReturnsLotNotFoundOrNotOwnedError()
    {
        _repo.CanManage = false;
        var handler = new SetSlotMaintenanceCommandHandler(
            _repo, _outbox, _clock, NullLogger<SetSlotMaintenanceCommandHandler>.Instance, _notifier);

        var command = new SetSlotMaintenanceCommand(
            _lotId, _slotId, _ownerId, "ParkingLotOwner", UnderMaintenance: true);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SlotMaintenanceErrors.LotNotFoundOrNotOwned.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_SlotNotFound_ReturnsSlotNotFoundError()
    {
        _repo.SlotToReturn = null;
        var handler = new SetSlotMaintenanceCommandHandler(
            _repo, _outbox, _clock, NullLogger<SetSlotMaintenanceCommandHandler>.Instance, _notifier);

        var command = new SetSlotMaintenanceCommand(
            _lotId, _slotId, _ownerId, "ParkingLotOwner", UnderMaintenance: true);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SlotMaintenanceErrors.SlotNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_ActiveSlotCannotBePlacedUnderMaintenance_ReturnsCannotMaintainActiveSlotError()
    {
        var slot = ParkingSlot.Create(_slotId, _lotId, _zoneId, "A-01");
        slot.UpdateSensorState(SensorState.Occupied);
        _repo.SlotToReturn = slot;

        var handler = new SetSlotMaintenanceCommandHandler(
            _repo, _outbox, _clock, NullLogger<SetSlotMaintenanceCommandHandler>.Instance, _notifier);

        var command = new SetSlotMaintenanceCommand(
            _lotId, _slotId, _ownerId, "ParkingLotOwner", UnderMaintenance: true);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SlotMaintenanceErrors.CannotMaintainActiveSlot.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Handle_ValidUnderMaintenanceRequest_UpdatesSlotStatusAndBroadcastsSignalR()
    {
        var slot = ParkingSlot.Create(_slotId, _lotId, _zoneId, "A-01");
        _repo.SlotToReturn = slot;
        _repo.AvailableCarSlotsToReturn = 9;

        var handler = new SetSlotMaintenanceCommandHandler(
            _repo, _outbox, _clock, NullLogger<SetSlotMaintenanceCommandHandler>.Instance, _notifier);

        var command = new SetSlotMaintenanceCommand(
            _lotId, _slotId, _ownerId, "ParkingLotOwner", UnderMaintenance: true, Reason: "Broken sensor");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Maintenance", result.Value!.EffectiveStatus);
        Assert.Equal("Maintenance", result.Value!.OverrideState);
        Assert.True(_repo.SaveChangesCalled);

        // Kiểm tra Outbox event được phát
        Assert.Single(_outbox.EnqueuedEvents);
        var integrationEvent = Assert.IsType<SlotStatusChangedIntegrationEvent>(_outbox.EnqueuedEvents[0]);
        Assert.Equal("Maintenance", integrationEvent.EffectiveStatus);
        Assert.Equal("Available", integrationEvent.PreviousStatus);

        // Kiểm tra SignalR broadcast
        Assert.Single(_notifier.SlotChanges);
        Assert.Equal((_lotId, _slotId, "Maintenance"), _notifier.SlotChanges[0]);
        Assert.Single(_notifier.OccupancyChanges);
        Assert.Equal((_lotId, 9), _notifier.OccupancyChanges[0]);
    }

    [Fact]
    public async Task Handle_ClearMaintenanceRequest_RevertsSlotStatusAndBroadcastsSignalR()
    {
        var slot = ParkingSlot.Create(_slotId, _lotId, _zoneId, "A-01", OverrideState.Maintenance);
        _repo.SlotToReturn = slot;
        _repo.AvailableCarSlotsToReturn = 10;

        var handler = new SetSlotMaintenanceCommandHandler(
            _repo, _outbox, _clock, NullLogger<SetSlotMaintenanceCommandHandler>.Instance, _notifier);

        var command = new SetSlotMaintenanceCommand(
            _lotId, _slotId, _ownerId, "ParkingLotOwner", UnderMaintenance: false);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Available", result.Value!.EffectiveStatus);
        Assert.Null(result.Value!.OverrideState);

        Assert.Single(_notifier.SlotChanges);
        Assert.Equal((_lotId, _slotId, "Available"), _notifier.SlotChanges[0]);
    }

    [Fact]
    public async Task Handle_AdminRole_AllowsManagingLotRegardlessOfAssignment()
    {
        _repo.CanManage = false; // Assignment fails, but role is Admin
        var slot = ParkingSlot.Create(_slotId, _lotId, _zoneId, "A-01");
        _repo.SlotToReturn = slot;

        var handler = new SetSlotMaintenanceCommandHandler(
            _repo, _outbox, _clock, NullLogger<SetSlotMaintenanceCommandHandler>.Instance, _notifier);

        var command = new SetSlotMaintenanceCommand(
            _lotId, _slotId, Guid.NewGuid(), "Admin", UnderMaintenance: true);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Maintenance", result.Value!.EffectiveStatus);
    }
}
