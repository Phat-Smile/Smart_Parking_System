using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Parking;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;
using ParkingSystem.Parking.Application.Common.Persistence;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Application.ParkingSessions.CompleteParkingSession;
using ParkingSystem.Parking.Application.ParkingSessions.GetParkingSession;
using ParkingSystem.Parking.Application.ParkingSessions.MarkParkingSessionPaid;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

// US-PAY-006 (BR-09.d, BR-11.c, BR-11.e): a vehicle leaves only with plate + QR + payment, inside the 15-minute window.
public sealed class CompleteParkingSessionTests
{
    private const string Plate = "51A-123.45";
    private const string Qr = "session-qr";
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ZoneId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid StaffId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset EntryTime = new(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PaidAt = EntryTime.AddHours(2);

    [Theory]
    [InlineData(0, true)]
    [InlineData(14 * 60 + 59, true)]
    [InlineData(15 * 60, true)] // exactly T + 15:00 is still in time
    [InlineData(15 * 60 + 1, false)] // T + 15:01 is late
    [InlineData(60 * 60, false)]
    public void Exit_window_is_15_minutes_after_payment(int secondsAfterPayment, bool canExit)
    {
        var session = PaidSession();

        Assert.Equal(canExit, ExitEligibility.CanExit(session, true, true, PaidAt.AddSeconds(secondsAfterPayment)));
    }

    [Fact]
    public void Only_an_active_session_can_be_marked_paid()
    {
        var session = PaidSession();

        Assert.False(session.MarkPaid(PaidAt.AddMinutes(1)));
        Assert.Equal(PaidAt, session.LastPaidAt);
    }

    [Fact]
    public void An_unpaid_session_cannot_complete()
    {
        var session = NewSession();

        Assert.False(session.Complete(PaidAt));
        Assert.Equal(ParkingSessionStatus.Active, session.Status);
        Assert.Null(session.ExitTime);
    }

    [Fact]
    public void Paying_again_after_the_window_starts_a_new_window()
    {
        var session = PaidSession();
        Assert.True(session.ReopenAfterExitWindow());

        Assert.True(session.MarkPaid(PaidAt.AddMinutes(30)));

        Assert.Equal(PaidAt.AddMinutes(45), session.ExitWindowEndsAt);
    }

    [Fact]
    public async Task A_paid_car_leaves_gives_back_its_slot_and_publishes_session_closed()
    {
        var session = PaidSession();
        var context = new Context(session, now: PaidAt.AddMinutes(5));

        var result = await context.Complete();

        Assert.True(result.IsSuccess);
        Assert.Equal("Completed", result.Value!.Status);
        Assert.Equal(PaidAt.AddMinutes(5), result.Value.CompletedAt);
        Assert.Equal(PaidAt.AddMinutes(5), session.ExitTime);
        Assert.Equal(1, context.Slots.Released);
        Assert.Equal(0, context.Zones.Released);
        Assert.True(context.UnitOfWork.Committed);

        Assert.Single(context.Outbox.Events);

        var closed = Assert.IsType<ParkingSessionClosedIntegrationEvent>(context.Outbox.Events[0].Event);
        Assert.Equal("parking.session-closed.v1", context.Outbox.Events[0].EventType);
        Assert.Equal(session.Id, closed.SessionId);
        Assert.Equal(Plate, closed.PlateNumber);
        Assert.Equal("Car", closed.VehicleType);
        Assert.Equal(EntryTime, closed.EntryTimeUtc);
        Assert.Equal(PaidAt.AddMinutes(5), closed.ExitTimeUtc);
    }

    [Fact]
    public async Task A_paid_motorbike_gives_back_one_space_of_its_zone()
    {
        var session = PaidSession(VehicleType.Motorbike);
        var context = new Context(session, now: PaidAt.AddMinutes(5));

        await context.Complete();

        Assert.Equal(1, context.Zones.Released);
        Assert.Equal(ZoneId, context.Zones.LastZoneId);
        Assert.Equal(0, context.Slots.Released);
    }

    [Fact]
    public async Task Repeating_complete_changes_nothing_and_returns_the_first_exit_time()
    {
        var session = PaidSession(VehicleType.Motorbike);
        var context = new Context(session, now: PaidAt.AddMinutes(5));
        await context.Complete();
        Assert.Single(context.Outbox.Events);
        context.Clock.Now = PaidAt.AddMinutes(9);

        var repeated = await context.Complete();

        Assert.True(repeated.IsSuccess);
        Assert.Equal(PaidAt.AddMinutes(5), repeated.Value!.CompletedAt);
        Assert.Equal(1, context.Zones.Released);
        Assert.Single(context.Outbox.Events);
    }

    [Fact]
    public async Task Repeating_complete_does_not_enqueue_duplicate_events()
    {
        var session = PaidSession();
        var context = new Context(session, now: PaidAt.AddMinutes(5));

        var first = await context.Complete();
        Assert.True(first.IsSuccess);
        Assert.Single(context.Outbox.Events);

        var second = await context.Complete();
        Assert.True(second.IsSuccess);
        Assert.Single(context.Outbox.Events);
    }

    [Fact]
    public async Task An_unpaid_session_gets_payment_required()
    {
        var session = NewSession();
        var context = new Context(session, now: PaidAt);

        var result = await context.Complete();

        Assert.Equal(CompleteParkingSessionErrors.PaymentRequired, result.Error);
        Assert.Equal(ParkingSessionStatus.Active, session.Status);
        Assert.Equal(0, context.Slots.Released);
        Assert.Empty(context.Outbox.Events);
    }

    [Fact]
    public async Task Late_at_the_gate_goes_back_to_active_and_keeps_the_barrier_closed()
    {
        var session = PaidSession();
        var context = new Context(session, now: PaidAt.AddMinutes(15).AddSeconds(1));

        var result = await context.Complete();

        Assert.Equal(CompleteParkingSessionErrors.ExitWindowExpired, result.Error);
        Assert.Equal(ParkingSessionStatus.Active, session.Status);
        Assert.Equal(PaidAt, session.LastPaidAt);
        Assert.Equal(1, context.Sessions.SaveCount);
        Assert.True(context.UnitOfWork.Committed);
        Assert.Equal(0, context.Slots.Released);
        Assert.Empty(context.Outbox.Events);
    }

    [Theory]
    [InlineData("51A-999.99", Qr)] // a plate match is required
    [InlineData(Plate, "other-qr")] // a plate match alone is not enough (BR-11.c)
    public async Task A_wrong_plate_or_qr_goes_to_staff(string plate, string qr)
    {
        var session = PaidSession();
        var context = new Context(session, now: PaidAt.AddMinutes(1));

        var result = await context.Complete(plate, qr);

        Assert.Equal(CompleteParkingSessionErrors.ExitNotVerified, result.Error);
        Assert.Equal(ParkingSessionStatus.Paid, session.Status);
    }

    [Fact]
    public async Task Staff_reason_lets_a_misread_plate_leave_and_is_audited_in_the_same_transaction()
    {
        // US-STAFF-004 (BR-11.d): the gate read the plate wrong, staff checked the vehicle in person.
        var session = PaidSession();
        var context = new Context(session, now: PaidAt.AddMinutes(1));

        var result = await context.Complete("51A-999.99", Qr, "  OCR misread, checked the car in person  ");

        Assert.True(result.IsSuccess);
        Assert.Equal(ParkingSessionStatus.Completed, session.Status);
        Assert.Equal(1, context.Slots.Released);
        Assert.Single(context.Outbox.Events);
        var entry = Assert.Single(context.Audit.Entries);
        Assert.Equal(AuditActions.SessionExitManuallyVerified, entry.Action);
        Assert.Equal(StaffId, entry.ActorUserId);
        Assert.Equal("OCR misread, checked the car in person", entry.Reason);
        Assert.Equal("False", entry.Details!["plateMatched"]);
        Assert.Equal("True", entry.Details["canExit"]);
        // NFR-15: no plate value in the audit details.
        Assert.DoesNotContain(entry.Details.Values, value => value.Contains("51A", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, context.Sessions.SaveCount);
        Assert.True(context.UnitOfWork.Committed);
    }

    [Fact]
    public async Task Staff_reason_never_replaces_the_qr_match()
    {
        var session = PaidSession();
        var context = new Context(session, now: PaidAt.AddMinutes(1));

        var result = await context.Complete("51A-999.99", "other-qr", "checked in person");

        Assert.Equal(CompleteParkingSessionErrors.ExitNotVerified, result.Error);
        Assert.Equal(ParkingSessionStatus.Paid, session.Status);
        Assert.Empty(context.Audit.Entries);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task A_blank_reason_does_not_count_as_a_manual_check(string? reason)
    {
        var session = PaidSession();
        var context = new Context(session, now: PaidAt.AddMinutes(1));

        var result = await context.Complete("51A-999.99", Qr, reason);

        Assert.Equal(CompleteParkingSessionErrors.ExitNotVerified, result.Error);
        Assert.Empty(context.Audit.Entries);
    }

    [Fact]
    public async Task Staff_reason_never_skips_payment()
    {
        var session = NewSession();
        var context = new Context(session, now: PaidAt);

        var result = await context.Complete("51A-999.99", Qr, "checked in person");

        Assert.Equal(CompleteParkingSessionErrors.PaymentRequired, result.Error);
        Assert.Equal(ParkingSessionStatus.Active, session.Status);
        Assert.Empty(context.Audit.Entries);
    }

    [Fact]
    public async Task Staff_reason_after_the_exit_window_reopens_the_session_and_is_audited()
    {
        var session = PaidSession();
        var context = new Context(session, now: PaidAt.AddMinutes(16));

        var result = await context.Complete("51A-999.99", Qr, "checked in person");

        Assert.Equal(CompleteParkingSessionErrors.ExitWindowExpired, result.Error);
        Assert.Equal(ParkingSessionStatus.Active, session.Status);
        Assert.Equal(0, context.Slots.Released);
        var entry = Assert.Single(context.Audit.Entries);
        Assert.Equal("False", entry.Details!["canExit"]);
    }

    [Fact]
    public async Task A_matching_plate_with_a_reason_is_not_audited()
    {
        var context = new Context(PaidSession(), now: PaidAt.AddMinutes(1));

        var result = await context.Complete(Plate, Qr, "not needed");

        Assert.True(result.IsSuccess);
        Assert.Empty(context.Audit.Entries);
    }

    [Fact]
    public async Task The_plate_is_normalized_like_at_entry()
    {
        var context = new Context(PaidSession(), now: PaidAt.AddMinutes(1));

        var result = await context.Complete(" 51a-123.45 ", Qr);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task A_cancelled_session_cannot_complete()
    {
        var session = NewSession();
        session.Cancel(StaffId, "did not enter", EntryTime);

        var result = await new Context(session, now: PaidAt).Complete();

        Assert.Equal(CompleteParkingSessionErrors.SessionNotOpen, result.Error);
    }

    [Fact]
    public async Task Unknown_session_is_not_found()
    {
        var result = await new Context(null, now: PaidAt).Complete();

        Assert.Equal(CompleteParkingSessionErrors.SessionNotFound, result.Error);
    }

    [Fact]
    public async Task Staff_not_assigned_to_the_lot_are_refused()
    {
        var session = PaidSession();
        var context = new Context(session, now: PaidAt, allowed: false);

        var result = await context.Complete();

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
        Assert.Equal(ParkingSessionStatus.Paid, session.Status);
    }

    [Theory]
    [InlineData("", Qr)]
    [InlineData("   ", Qr)]
    [InlineData(Plate, "")]
    public void Validator_requires_plate_and_qr(string plate, string qr)
    {
        var validator = new CompleteParkingSessionCommandValidator();

        Assert.False(validator.Validate(new CompleteParkingSessionCommand(Guid.NewGuid(), plate, qr, StaffId)).IsValid);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("checked in person", true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Validator_caps_the_staff_reason_at_500_characters(object? reason, bool valid)
    {
        var text = reason is int length ? new string('x', length) : (string?)reason;
        var validator = new CompleteParkingSessionCommandValidator();

        Assert.Equal(valid, validator.Validate(new CompleteParkingSessionCommand(Guid.NewGuid(), Plate, Qr, StaffId, text)).IsValid);
    }

    [Fact]
    public async Task Payment_event_marks_the_session_paid()
    {
        var session = NewSession();
        var repository = new FakeSessions(session);

        var result = await new MarkParkingSessionPaidCommandHandler(repository)
            .Handle(new MarkParkingSessionPaidCommand(session.Id, PaidAt), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ParkingSessionStatus.Paid, session.Status);
        Assert.Equal(PaidAt, session.LastPaidAt);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task A_payment_event_for_a_session_no_longer_active_changes_nothing()
    {
        var session = PaidSession();
        var repository = new FakeSessions(session);

        var result = await new MarkParkingSessionPaidCommandHandler(repository)
            .Handle(new MarkParkingSessionPaidCommand(session.Id, PaidAt.AddMinutes(3)), TestContext.Current.CancellationToken);

        Assert.Equal(MarkParkingSessionPaidErrors.SessionNotActive, result.Error);
        Assert.Equal(PaidAt, session.LastPaidAt);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task A_payment_event_for_an_unknown_session_is_not_found()
    {
        var result = await new MarkParkingSessionPaidCommandHandler(new FakeSessions(null))
            .Handle(new MarkParkingSessionPaidCommand(Guid.NewGuid(), PaidAt), TestContext.Current.CancellationToken);

        Assert.Equal(MarkParkingSessionPaidErrors.SessionNotFound, result.Error);
    }

    [Fact]
    public async Task Session_detail_shows_the_payment_time_and_the_end_of_the_window()
    {
        var session = PaidSession();
        var handler = new GetParkingSessionQueryHandler(new FakeSessions(session), new FakeLotAccess(true), new FakeSlots());

        var result = await handler.Handle(new GetParkingSessionQuery(session.Id, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(PaidAt, result.Value!.PaidAtUtc);
        Assert.Equal(PaidAt.AddMinutes(15), result.Value.GraceUntilUtc);
    }

    private static ParkingSession NewSession(VehicleType vehicleType = VehicleType.Car) =>
        ParkingSession.Open(Guid.NewGuid(), LotId, Plate, vehicleType, EntryTime, Qr, null, null, ZoneId);

    private static ParkingSession PaidSession(VehicleType vehicleType = VehicleType.Car)
    {
        var session = NewSession(vehicleType);
        Assert.True(session.MarkPaid(PaidAt));
        return session;
    }

    private sealed class Context(ParkingSession? session, DateTimeOffset now, bool allowed = true)
    {
        public FakeSessions Sessions { get; } = new(session);
        public FakeUnitOfWork UnitOfWork { get; } = new();
        public FakeSlots Slots { get; } = new();
        public FakeZones Zones { get; } = new();
        public FakeOutbox Outbox { get; } = new();
        public FakeAuditLog Audit { get; } = new();
        public FakeClock Clock { get; } = new() { Now = now };

        public Task<ParkingSystem.BuildingBlocks.Results.Result<CompletedParkingSession>> Complete(
            string plate = Plate, string qr = Qr, string? manualReason = null)
        {
            var handler = new CompleteParkingSessionCommandHandler(Sessions, UnitOfWork, new FakeLotAccess(allowed), Slots, Zones, Outbox, Audit, Clock);
            return handler.Handle(
                new CompleteParkingSessionCommand(session?.Id ?? Guid.NewGuid(), plate, qr, StaffId, manualReason),
                TestContext.Current.CancellationToken);
        }
    }

    private sealed class FakeSessions(ParkingSession? session) : IParkingSessionRepository
    {
        public int SaveCount { get; private set; }

        public Task<ParkingSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(session?.Id == sessionId ? session : null);

        public Task<ParkingSession?> FindByIdForUpdateAsync(Guid sessionId, CancellationToken cancellationToken) =>
            FindByIdAsync(sessionId, cancellationToken);

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(true);
        }

        public Task<bool> HasOpenSessionAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<ParkingSession>> ListOpenByUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ParkingSession?> FindOpenByQrTokenAsync(string qrToken, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<(IReadOnlyList<ParkingSession> Items, int TotalCount)> ListByLotAsync(
            Guid lotId, ParkingSessionStatus? status, int skip, int take, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ParkingSession?> FindOpenByPlateAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Add(ParkingSession added) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IParkingUnitOfWork, IParkingTransaction
    {
        public bool Committed { get; private set; }

        public Task<IParkingTransaction> BeginTransactionAsync(CancellationToken cancellationToken) => Task.FromResult<IParkingTransaction>(this);

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeSlots : ISlotDesignator
    {
        public int Released { get; private set; }

        public Task<string?> ReleaseSessionSlotAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            Released++;
            return Task.FromResult<string?>("A-01");
        }

        public Task<IReadOnlyDictionary<Guid, string>> FindSlotCodesAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

        public Task<DesignatedSlot?> DesignateCarSlotAsync(Guid lotId, Guid sessionId, DateTimeOffset assignedAt, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> LockLotAndCountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeZones : IMotorbikeZoneCounter
    {
        public int Released { get; private set; }
        public Guid? LastZoneId { get; private set; }

        public Task ReleaseSpaceAsync(Guid zoneId, CancellationToken cancellationToken)
        {
            Released++;
            LastZoneId = zoneId;
            return Task.CompletedTask;
        }

        public Task<MotorbikeSpaceTaken?> TakeSpaceAsync(Guid lotId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MotorbikeAvailability?> GetAvailabilityAsync(Guid lotId, CancellationToken cancellationToken) => throw new NotSupportedException();
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

    private sealed class FakeAuditLog : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = [];

        public void Record(AuditEntry entry) => Entries.Add(entry);
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset Now { get; set; }
        public DateTimeOffset UtcNow => Now;
    }
}
