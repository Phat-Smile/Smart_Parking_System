using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Parking;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;
using ParkingSystem.Parking.Application.ParkingLayout.SlotConfirmation;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Domain.Incidents;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

// US-PARK-005 (BR-04.c, BR-04.e, transition table after BR-06).
public sealed class SlotConfirmationTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ZoneId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid SessionId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid StaffId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset AssignedAt = new(2026, 11, 6, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = AssignedAt.AddMinutes(11);

    [Fact]
    public void Assigned_slot_without_sensor_becomes_occupied_and_keeps_its_session()
    {
        var slot = AssignedSlot();

        Assert.True(slot.ConfirmParked(zoneHasSlotSensor: false, Now));

        Assert.Equal(SlotEffectiveStatus.Occupied, slot.EffectiveStatus);
        Assert.Null(slot.DispatchState);
        Assert.Equal(Now, slot.ManuallyParkedAt);
        Assert.Equal(SessionId, slot.AssignedSessionId);
        Assert.Null(slot.SensorState); // the sensor layer belongs to sensor events only (BR-06.a)
    }

    [Fact]
    public void Assigned_slot_with_sensor_cannot_be_confirmed_by_staff()
    {
        var slot = AssignedSlot();

        Assert.False(slot.ConfirmParked(zoneHasSlotSensor: true, Now));

        Assert.Equal(SlotEffectiveStatus.Assigned, slot.EffectiveStatus);
    }

    [Fact]
    public void Needs_verification_can_be_confirmed_parked_in_a_zone_with_sensors()
    {
        var slot = NeedsVerificationSlot();

        Assert.True(slot.ConfirmParked(zoneHasSlotSensor: true, Now));

        Assert.Equal(SlotEffectiveStatus.Occupied, slot.EffectiveStatus);
        Assert.Null(slot.NeedsVerificationSince);
    }

    [Fact]
    public void A_free_slot_cannot_be_confirmed()
    {
        var slot = ParkingSlot.Create(Guid.NewGuid(), LotId, ZoneId, "A-01");

        Assert.False(slot.ConfirmParked(zoneHasSlotSensor: false, Now));
        Assert.False(slot.ConfirmEmpty());
    }

    [Fact]
    public void Sensor_occupied_at_an_assigned_slot_makes_it_occupied_and_keeps_its_session()
    {
        var slot = AssignedSlot();

        slot.UpdateSensorState(SensorState.Occupied);

        Assert.Equal(SlotEffectiveStatus.Occupied, slot.EffectiveStatus);
        Assert.Null(slot.DispatchState);
        Assert.Equal(SessionId, slot.AssignedSessionId);
    }

    [Fact]
    public void Sensor_empty_does_not_end_an_assignment()
    {
        var slot = AssignedSlot();

        slot.UpdateSensorState(SensorState.Empty);

        Assert.Equal(SlotEffectiveStatus.Assigned, slot.EffectiveStatus); // BR-06.e
    }

    [Fact]
    public void Sensor_occupied_does_not_end_needs_verification()
    {
        var slot = NeedsVerificationSlot();

        slot.UpdateSensorState(SensorState.Occupied);

        Assert.Equal(SlotEffectiveStatus.NeedsVerification, slot.EffectiveStatus);
    }

    [Fact]
    public void Leaving_clears_the_manual_confirmation_and_frees_the_slot()
    {
        var slot = AssignedSlot();
        slot.ConfirmParked(zoneHasSlotSensor: false, Now);

        slot.ReleaseAssignment();

        Assert.Equal(SlotEffectiveStatus.Available, slot.EffectiveStatus);
        Assert.Null(slot.ManuallyParkedAt);
        Assert.Null(slot.AssignedSessionId);
    }

    [Fact]
    public async Task Confirm_parked_writes_audit_resolves_the_incident_and_publishes_the_status()
    {
        var slot = NeedsVerificationSlot();
        var incident = Incident.OpenParkConfirmTimeout(Guid.NewGuid(), LotId, SessionId, slot.Id, AssignedAt);
        var context = new Context(slot, incident);

        var result = await context.ConfirmParked("car parked one slot off");

        Assert.Equal("Occupied", result.Value!.EffectiveStatus);
        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.Equal("car parked one slot off", incident.ResolutionReason);
        var entry = Assert.Single(context.Audit.Entries);
        Assert.Equal(AuditActions.SlotParkedConfirmed, entry.Action);
        Assert.Equal("car parked one slot off", entry.Reason);
        Assert.Equal("NeedsVerification", entry.Details!["fromStatus"]);
        Assert.Equal("parking.slot-status-changed.v1", Assert.Single(context.Outbox.Events).EventType);
    }

    [Fact]
    public async Task Confirm_parked_is_refused_when_not_confirmable_or_not_assigned_to_the_lot()
    {
        var sensorZone = new Context(AssignedSlot(), incident: null, zoneHasSensor: true);
        var otherLot = new Context(AssignedSlot(), incident: null, allowed: false);

        Assert.Equal(ConfirmSlotErrors.NotConfirmable, (await sensorZone.ConfirmParked(null)).Error);
        Assert.Equal(ParkingLotAccessErrors.AccessDenied, (await otherLot.ConfirmParked(null)).Error);
        Assert.Empty(sensorZone.Audit.Entries);
    }

    [Fact]
    public async Task Unknown_slot_is_not_found()
    {
        Assert.Equal(ConfirmSlotErrors.SlotNotFound, (await new Context(null, null).ConfirmParked(null)).Error);
    }

    [Fact]
    public async Task The_second_of_two_staff_gets_slot_changed()
    {
        var context = new Context(NeedsVerificationSlot(), null) { };
        context.Slots.SaveSucceeds = false;

        Assert.Equal(ConfirmSlotErrors.SlotChanged, (await context.ConfirmParked(null)).Error);
    }

    [Fact]
    public async Task Confirm_empty_with_an_open_session_frees_the_slot_keeps_the_session_and_the_incident()
    {
        var slot = NeedsVerificationSlot();
        var incident = Incident.OpenParkConfirmTimeout(Guid.NewGuid(), LotId, SessionId, slot.Id, AssignedAt);
        var session = OpenSession();
        var context = new Context(slot, incident, session: session);

        var result = await context.ConfirmEmpty("no car in this slot");

        Assert.Equal("Available", result.Value!.EffectiveStatus);
        Assert.Null(slot.AssignedSessionId);
        Assert.Equal(ParkingSessionStatus.Active, session.Status);
        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.Equal(AuditActions.SlotEmptyConfirmed, Assert.Single(context.Audit.Entries).Action);
    }

    [Fact]
    public async Task Confirm_empty_with_an_open_session_and_no_incident_opens_one()
    {
        var slot = NeedsVerificationSlot();
        var context = new Context(slot, incident: null, session: OpenSession());

        await context.ConfirmEmpty("no car in this slot");

        var added = Assert.Single(context.Slots.Added);
        Assert.Equal(IncidentType.ParkConfirmTimeout, added.IncidentType);
        Assert.Equal(SessionId, added.SessionId);
        Assert.Equal(slot.Id, added.SlotId);
    }

    [Fact]
    public async Task Confirm_empty_without_an_open_session_resolves_the_incident()
    {
        var slot = NeedsVerificationSlot();
        var incident = Incident.OpenParkConfirmTimeout(Guid.NewGuid(), LotId, SessionId, slot.Id, AssignedAt);

        await new Context(slot, incident).ConfirmEmpty("session already gone");

        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.Equal("session already gone", incident.ResolutionReason);
    }

    [Fact]
    public async Task Confirm_empty_needs_a_slot_waiting_for_verification()
    {
        var result = await new Context(AssignedSlot(), null).ConfirmEmpty("no car");

        Assert.Equal(ConfirmSlotErrors.NotNeedsVerification, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Confirm_empty_requires_a_reason(string reason)
    {
        Assert.False(new ConfirmSlotEmptyCommandValidator().Validate(new ConfirmSlotEmptyCommand(Guid.NewGuid(), reason, StaffId)).IsValid);
    }

    [Fact]
    public void Confirm_parked_reason_is_optional_but_limited()
    {
        var validator = new ConfirmSlotParkedCommandValidator();

        Assert.True(validator.Validate(new ConfirmSlotParkedCommand(Guid.NewGuid(), null, StaffId)).IsValid);
        Assert.False(validator.Validate(new ConfirmSlotParkedCommand(Guid.NewGuid(), new string('x', 501), StaffId)).IsValid);
    }

    [Fact]
    public async Task The_timeout_moves_the_slot_opens_an_incident_and_tells_staff()
    {
        var slot = AssignedSlot();
        var repository = new FakeSlots(null, null) { Overdue = [slot] };
        var outbox = new FakeOutbox();

        var result = await new CheckParkConfirmTimeoutsCommandHandler(repository, outbox, new FixedClock())
            .Handle(new CheckParkConfirmTimeoutsCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Value!.MovedToNeedsVerification);
        Assert.Equal(Now - ParkingSlot.ParkConfirmTimeout, repository.AssignedCutoff);
        Assert.Equal(SlotEffectiveStatus.NeedsVerification, slot.EffectiveStatus);
        Assert.Equal(Now, slot.NeedsVerificationSince);
        var incident = Assert.Single(repository.Added);
        Assert.Equal(IncidentType.ParkConfirmTimeout, incident.IncidentType);
        Assert.Equal(AssignedAt, incident.OccurredAt);
        var published = outbox.Events.Select(item => item.Event).OfType<ParkingSlotNeedsVerificationIntegrationEvent>().Single();
        Assert.Equal("Staff", published.Stage);
        Assert.Equal(incident.Id, published.IncidentId);
    }

    [Fact]
    public async Task After_30_minutes_the_owner_is_told_once()
    {
        var slot = NeedsVerificationSlot();
        var repository = new FakeSlots(null, null) { OwnerDue = [slot] };
        var outbox = new FakeOutbox();
        var handler = new CheckParkConfirmTimeoutsCommandHandler(repository, outbox, new FixedClock());

        var result = await handler.Handle(new CheckParkConfirmTimeoutsCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Value!.OwnersNotified);
        Assert.Equal(Now - ParkingSlot.OwnerNotifyDelay, repository.OwnerCutoff);
        Assert.Equal(Now, slot.OwnerNotifiedAt);
        Assert.Equal("Owner", outbox.Events.Select(item => item.Event).OfType<ParkingSlotNeedsVerificationIntegrationEvent>().Single().Stage);
    }

    [Fact]
    public async Task A_slot_changed_by_staff_stops_the_run_and_the_next_tick_continues()
    {
        var repository = new FakeSlots(null, null) { Overdue = [AssignedSlot(), AssignedSlot()], SaveSucceeds = false };

        var result = await new CheckParkConfirmTimeoutsCommandHandler(repository, new FakeOutbox(), new FixedClock())
            .Handle(new CheckParkConfirmTimeoutsCommand(), TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Value!.MovedToNeedsVerification);
        Assert.Equal(1, repository.SaveCount);
    }

    private static ParkingSlot AssignedSlot()
    {
        var slot = ParkingSlot.Create(Guid.NewGuid(), LotId, ZoneId, "A-01");
        slot.AssignSession(SessionId, AssignedAt);
        return slot;
    }

    private static ParkingSlot NeedsVerificationSlot()
    {
        var slot = AssignedSlot();
        slot.MarkNeedsVerification(AssignedAt.AddMinutes(10));
        return slot;
    }

    private static ParkingSession OpenSession() =>
        ParkingSession.Open(SessionId, LotId, "51A-123.45", VehicleType.Car, AssignedAt, "qr", null, null);

    private sealed class Context(ParkingSlot? slot, Incident? incident, bool zoneHasSensor = false, bool allowed = true, ParkingSession? session = null)
    {
        public FakeSlots Slots { get; } = new(slot, incident, zoneHasSensor);
        public FakeAuditLog Audit { get; } = new();
        public FakeOutbox Outbox { get; } = new();

        public Task<ParkingSystem.BuildingBlocks.Results.Result<ConfirmedSlot>> ConfirmParked(string? reason) =>
            new ConfirmSlotParkedCommandHandler(Slots, new FakeLotAccess(allowed), Audit, Outbox, new FixedClock())
                .Handle(new ConfirmSlotParkedCommand(slot?.Id ?? Guid.NewGuid(), reason, StaffId), TestContext.Current.CancellationToken);

        public Task<ParkingSystem.BuildingBlocks.Results.Result<ConfirmedSlot>> ConfirmEmpty(string reason) =>
            new ConfirmSlotEmptyCommandHandler(Slots, new FakeSessions(session), new FakeLotAccess(allowed), Audit, Outbox, new FixedClock())
                .Handle(new ConfirmSlotEmptyCommand(slot?.Id ?? Guid.NewGuid(), reason, StaffId), TestContext.Current.CancellationToken);
    }

    private sealed class FakeSlots(ParkingSlot? slot, Incident? incident, bool zoneHasSensor = false) : ISlotConfirmationRepository
    {
        public IReadOnlyList<ParkingSlot> Overdue { get; init; } = [];
        public IReadOnlyList<ParkingSlot> OwnerDue { get; init; } = [];
        public bool SaveSucceeds { get; set; } = true;
        public int SaveCount { get; private set; }
        public List<Incident> Added { get; } = [];
        public DateTimeOffset? AssignedCutoff { get; private set; }
        public DateTimeOffset? OwnerCutoff { get; private set; }

        public Task<(ParkingSlot Slot, bool ZoneHasSlotSensor)?> FindWithZoneAsync(Guid slotId, CancellationToken cancellationToken) =>
            Task.FromResult<(ParkingSlot, bool)?>(slot?.Id == slotId ? (slot, zoneHasSensor) : null);

        public Task<IReadOnlyList<ParkingSlot>> ListAssignedBeforeAsync(DateTimeOffset cutoff, int take, CancellationToken cancellationToken)
        {
            AssignedCutoff = cutoff;
            return Task.FromResult(Overdue);
        }

        public Task<IReadOnlyList<ParkingSlot>> ListOwnerNotNotifiedBeforeAsync(DateTimeOffset cutoff, int take, CancellationToken cancellationToken)
        {
            OwnerCutoff = cutoff;
            return Task.FromResult(OwnerDue);
        }

        public Task<Incident?> FindOpenTimeoutIncidentAsync(Guid slotId, CancellationToken cancellationToken) => Task.FromResult(incident);

        public void AddIncident(Incident added) => Added.Add(added);

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(SaveSucceeds);
        }
    }

    private sealed class FakeSessions(ParkingSession? session) : IParkingSessionRepository
    {
        public Task<ParkingSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(session?.Id == sessionId ? session : null);

        public Task<ParkingSession?> FindByIdForUpdateAsync(Guid sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> HasOpenSessionAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<ParkingSession>> ListOpenByUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ParkingSession?> FindOpenByQrTokenAsync(string qrToken, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<(IReadOnlyList<ParkingSession> Items, int TotalCount)> ListByLotAsync(
            Guid lotId, ParkingSessionStatus? status, int skip, int take, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ParkingSession?> FindOpenByPlateAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Add(ParkingSession added) => throw new NotSupportedException();

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeAuditLog : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = [];

        public void Record(AuditEntry entry) => Entries.Add(entry);
    }

    private sealed class FakeOutbox : IOutboxWriter
    {
        public List<(object Event, string EventType)> Events { get; } = [];

        public void Enqueue<TEvent>(TEvent integrationEvent, string eventType, int eventVersion, Guid? correlationId = null, Guid? causationId = null)
            where TEvent : class => Events.Add((integrationEvent, eventType));
    }

    private sealed class FakeLotAccess(bool allowed) : IParkingLotAccess
    {
        public Task<bool> CanOperateAsync(Guid accountId, Guid lotId, CancellationToken cancellationToken) => Task.FromResult(allowed);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
