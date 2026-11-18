using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Parking;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;
using ParkingSystem.Parking.Application.Common.Persistence;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Application.ParkingSessions.CompleteParkingSession;
using ParkingSystem.Parking.Application.ParkingSessions.RecordExitArrival;
using ParkingSystem.Parking.Application.ParkingSessions.VerifyParkingSessionExit;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

// Unit tests verifying prevention of ExitArrivedAt security exploit vectors (Tin 121 & PR B).
public sealed class ExitArrivalSecurityTests
{
    private const string Plate = "51A-123.45";
    private const string Qr = "session-qr-token";
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid StaffId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset EntryTime = new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PaidAt = EntryTime.AddHours(2); // 10:00:00

    [Fact]
    public async Task Exploit1_Staff_verifies_exit_car_delays_2_hours_cannot_exit_free()
    {
        // Vector 1: Staff manually verifies exit at T0 + 5m (records ExitArrivedAt).
        // Car turns around and stays in the lot for 2 hours.
        // At T0 + 2h 5m, car tries to complete session -> must fail with ExitWindowExpired and reopen to Active!
        var session = NewPaidSession();
        var repository = new FakeSessionRepository(session);
        var auditLog = new FakeAuditLog();
        var verifyClock = new FakeClock { Now = PaidAt.AddMinutes(5) };

        var verifyHandler = new VerifyParkingSessionExitCommandHandler(
            repository, new FakeLotAccess(allowed: true), auditLog, verifyClock);

        var verifyResult = await verifyHandler.Handle(
            new VerifyParkingSessionExitCommand(session.Id, "Manual check at gate", StaffId),
            TestContext.Current.CancellationToken);

        Assert.True(verifyResult.IsSuccess);
        Assert.True(verifyResult.Value!.CanExit);
        Assert.Equal(PaidAt.AddMinutes(5), session.ExitArrivedAt);
        Assert.Equal(ParkingSessionStatus.Paid, session.Status);

        // 2 hours pass; car returns to gate and attempts to complete
        var departureTime = PaidAt.AddMinutes(5).AddHours(2); // 12:05:00
        var completeContext = new CompleteContext(session, departureTime);

        var completeResult = await completeContext.Complete();

        Assert.False(completeResult.IsSuccess);
        Assert.Equal(CompleteParkingSessionErrors.ExitWindowExpired, completeResult.Error);
        Assert.Equal(ParkingSessionStatus.Active, session.Status); // Reopened to Active for overstay payment
        Assert.Null(session.ExitArrivedAt); // Stale arrival reset
        Assert.False(session.HasExitWindowExtension); // Extension reset
        Assert.Equal(0, completeContext.Slots.Released);
    }

    [Fact]
    public void Exploit2_Reopen_and_pay_overstay_fee_clears_old_arrival_preventing_negative_offset_exploit()
    {
        // Vector 2: Vehicle window expires -> ReopenAfterExitWindow sets Active.
        // Customer pays overstay fee -> MarkPaid updates LastPaidAt to new timestamp.
        // Old ExitArrivedAt MUST be cleared; otherwise ExitArrivedAt - LastPaidAt is negative, allowing indefinite free exit.
        var session = NewPaidSession();
        var firstArrival = PaidAt.AddMinutes(10);
        Assert.True(session.RecordExitArrival(firstArrival));
        Assert.Equal(firstArrival, session.ExitArrivedAt);

        // Window expired at gate -> reopen to Active
        Assert.True(session.ReopenAfterExitWindow());
        Assert.Equal(ParkingSessionStatus.Active, session.Status);
        Assert.Null(session.ExitArrivedAt); // Reset to prevent negative offset
        Assert.False(session.HasExitWindowExtension);

        // Customer pays overstay fee at T1 = PaidAt + 40m
        var newPaidAt = PaidAt.AddMinutes(40);
        Assert.True(session.MarkPaid(newPaidAt));
        Assert.Equal(ParkingSessionStatus.Paid, session.Status);
        Assert.Equal(newPaidAt, session.LastPaidAt);
        Assert.Null(session.ExitArrivedAt); // Verified null

        // If vehicle now delays 25 minutes after new payment without reaching gate on time:
        var checkTime = newPaidAt.AddMinutes(25);
        Assert.True(session.IsExitWindowOver(checkTime));
        Assert.False(ExitEligibility.CanExit(session, plateMatches: true, qrMatches: true, checkTime));
    }

    [Fact]
    public async Task Valid_exit_arrival_within_15_minutes_successfully_completes()
    {
        // Legitimate departure: vehicle arrives at gate at T0 + 10m (<= 15m) and completes at T0 + 13m
        var session = NewPaidSession();
        var arrivalTime = PaidAt.AddMinutes(10);
        Assert.True(session.RecordExitArrival(arrivalTime));

        var exitTime = PaidAt.AddMinutes(13);
        var completeContext = new CompleteContext(session, exitTime);

        var result = await completeContext.Complete();

        Assert.True(result.IsSuccess);
        Assert.Equal(ParkingSessionStatus.Completed, session.Status);
        Assert.Equal(exitTime, session.ExitTime);
        Assert.Equal(1, completeContext.Slots.Released);
        Assert.True(completeContext.UnitOfWork.Committed);
    }

    [Fact]
    public async Task Linger_greater_than_15_minutes_at_gate_is_treated_as_overstay()
    {
        // Vehicle reaches gate at T0 + 10m, but lingers at gate or motor barrier delay for 16 minutes (> 15m linger limit)
        var session = NewPaidSession();
        var arrivalTime = PaidAt.AddMinutes(10);
        Assert.True(session.RecordExitArrival(arrivalTime));

        // 16 minutes after arrival at gate
        var checkTime = arrivalTime.AddMinutes(16); // PaidAt + 26m

        Assert.True(session.IsExitWindowOver(checkTime));
        Assert.False(session.Complete(checkTime));

        var completeContext = new CompleteContext(session, checkTime);
        var result = await completeContext.Complete();

        Assert.False(result.IsSuccess);
        Assert.Equal(CompleteParkingSessionErrors.ExitWindowExpired, result.Error);
        Assert.Equal(ParkingSessionStatus.Active, session.Status);
        Assert.Null(session.ExitArrivedAt);
    }

    [Fact]
    public void RecordExitArrival_is_rejected_when_session_is_not_paid()
    {
        var session = ParkingSession.Open(Guid.NewGuid(), LotId, Plate, VehicleType.Car, EntryTime, Qr, null, null);
        Assert.Equal(ParkingSessionStatus.Active, session.Status);

        var result = session.RecordExitArrival(PaidAt);

        Assert.False(result);
        Assert.Null(session.ExitArrivedAt);
    }

    [Fact]
    public void RecordExitArrival_is_rejected_when_exit_window_is_already_expired()
    {
        var session = NewPaidSession();
        var lateArrival = PaidAt.AddMinutes(15).AddSeconds(1);

        var result = session.RecordExitArrival(lateArrival);

        Assert.False(result);
        Assert.Null(session.ExitArrivedAt);
    }

    [Fact]
    public async Task RecordExitArrivalCommandHandler_records_arrival_for_paid_session()
    {
        var session = NewPaidSession();
        var repository = new FakeSessionRepository(session);
        var arrivalTime = PaidAt.AddMinutes(8);
        var clock = new FakeClock { Now = arrivalTime };
        var handler = new RecordExitArrivalCommandHandler(repository, new FakeLotAccess(allowed: true), clock);

        var result = await handler.Handle(
            new RecordExitArrivalCommand(session.Id, StaffId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(session.Id, result.Value!.SessionId);
        Assert.Equal(arrivalTime, result.Value.ExitArrivedAt);
        Assert.Equal(arrivalTime, session.ExitArrivedAt);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task RecordExitArrivalCommandHandler_returns_not_found_when_session_missing()
    {
        var repository = new FakeSessionRepository(null);
        var handler = new RecordExitArrivalCommandHandler(repository, new FakeLotAccess(allowed: true), new FakeClock());

        var result = await handler.Handle(
            new RecordExitArrivalCommand(Guid.NewGuid(), StaffId),
            TestContext.Current.CancellationToken);

        Assert.Equal(RecordExitArrivalErrors.SessionNotFound, result.Error);
    }

    [Fact]
    public async Task RecordExitArrivalCommandHandler_returns_access_denied_when_caller_not_permitted()
    {
        var session = NewPaidSession();
        var repository = new FakeSessionRepository(session);
        var handler = new RecordExitArrivalCommandHandler(repository, new FakeLotAccess(allowed: false), new FakeClock());

        var result = await handler.Handle(
            new RecordExitArrivalCommand(session.Id, StaffId),
            TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
        Assert.Null(session.ExitArrivedAt);
    }

    [Fact]
    public async Task RecordExitArrivalCommandHandler_returns_session_not_paid_when_active()
    {
        var session = ParkingSession.Open(Guid.NewGuid(), LotId, Plate, VehicleType.Car, EntryTime, Qr, null, null);
        var repository = new FakeSessionRepository(session);
        var handler = new RecordExitArrivalCommandHandler(repository, new FakeLotAccess(allowed: true), new FakeClock());

        var result = await handler.Handle(
            new RecordExitArrivalCommand(session.Id, StaffId),
            TestContext.Current.CancellationToken);

        Assert.Equal(RecordExitArrivalErrors.SessionNotPaid, result.Error);
        Assert.Null(session.ExitArrivedAt);
    }

    [Fact]
    public async Task RecordExitArrivalCommandHandler_returns_window_expired_when_late()
    {
        var session = NewPaidSession();
        var repository = new FakeSessionRepository(session);
        var clock = new FakeClock { Now = PaidAt.AddMinutes(16) };
        var handler = new RecordExitArrivalCommandHandler(repository, new FakeLotAccess(allowed: true), clock);

        var result = await handler.Handle(
            new RecordExitArrivalCommand(session.Id, StaffId),
            TestContext.Current.CancellationToken);

        Assert.Equal(RecordExitArrivalErrors.ExitWindowExpired, result.Error);
        Assert.Equal(ParkingSessionStatus.Active, session.Status); // Reopened
        Assert.Null(session.ExitArrivedAt);
        Assert.Equal(1, repository.SaveCount);
    }

    private static ParkingSession NewPaidSession()
    {
        var session = ParkingSession.Open(Guid.NewGuid(), LotId, Plate, VehicleType.Car, EntryTime, Qr, null, null);
        Assert.True(session.MarkPaid(PaidAt));
        return session;
    }

    private sealed class CompleteContext(ParkingSession? session, DateTimeOffset now, bool allowed = true)
    {
        public FakeSessionRepository Sessions { get; } = new(session);
        public FakeUnitOfWork UnitOfWork { get; } = new();
        public FakeSlots Slots { get; } = new();
        public FakeMotorbikeZones Zones { get; } = new();
        public FakeOutbox Outbox { get; } = new();
        public FakeClock Clock { get; } = new() { Now = now };

        public Task<Result<CompletedParkingSession>> Complete(string plate = Plate, string qr = Qr)
        {
            var handler = new CompleteParkingSessionCommandHandler(
                Sessions, UnitOfWork, new FakeLotAccess(allowed), Slots, Zones, Outbox, new FakeAuditLog(), Clock);
            return handler.Handle(
                new CompleteParkingSessionCommand(session?.Id ?? Guid.NewGuid(), plate, qr, StaffId),
                TestContext.Current.CancellationToken);
        }
    }

    private sealed class FakeSessionRepository(ParkingSession? session) : IParkingSessionRepository
    {
        public int SaveCount { get; private set; }

        public Task<bool> HasOpenSessionAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ParkingSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(session?.Id == sessionId ? session : null);

        public Task<IReadOnlyList<ParkingSession>> ListOpenByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ParkingSession?> FindOpenByQrTokenAsync(string qrToken, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<ParkingSession> Items, int TotalCount)> ListByLotAsync(
            Guid lotId, ParkingSessionStatus? status, int skip, int take, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ParkingSession?> FindOpenByPlateAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ParkingSession?> FindByIdForUpdateAsync(Guid sessionId, CancellationToken cancellationToken) =>
            FindByIdAsync(sessionId, cancellationToken);

        public void Add(ParkingSession added) => throw new NotSupportedException();

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeLotAccess(bool allowed) : IParkingLotAccess
    {
        public Task<bool> CanOperateAsync(Guid accountId, Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult(allowed);
    }

    private sealed class FakeAuditLog : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = [];

        public void Record(AuditEntry entry) => Entries.Add(entry);
    }

    private sealed class FakeUnitOfWork : IParkingUnitOfWork, IParkingTransaction
    {
        public bool Committed { get; private set; }

        public Task<IParkingTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IParkingTransaction>(this);

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

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

        public Task<int> LockLotAndCountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeMotorbikeZones : IMotorbikeZoneCounter
    {
        public int Released { get; private set; }

        public Task ReleaseSpaceAsync(Guid zoneId, CancellationToken cancellationToken)
        {
            Released++;
            return Task.CompletedTask;
        }

        public Task<MotorbikeSpaceTaken?> TakeSpaceAsync(Guid lotId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<MotorbikeAvailability?> GetAvailabilityAsync(Guid lotId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeOutbox : IOutboxWriter
    {
        public List<(object Event, string EventType)> Events { get; } = [];

        public void Enqueue<TEvent>(TEvent integrationEvent, string eventType, int eventVersion, Guid? correlationId = null, Guid? causationId = null)
            where TEvent : class => Events.Add((integrationEvent, eventType));
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset Now { get; set; } = PaidAt;
        public DateTimeOffset UtcNow => Now;
    }
}
