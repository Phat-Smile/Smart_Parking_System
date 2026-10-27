using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Application.ParkingSessions.CancelParkingSession;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class CancelParkingSessionTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset EntryTime = new(2026, 10, 6, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = EntryTime.AddMinutes(2);
    private static readonly Guid StaffId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStaffId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly CancelParkingSessionCommandValidator validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_requires_a_reason(string reason)
    {
        var result = validator.Validate(new CancelParkingSessionCommand(Guid.NewGuid(), reason, StaffId));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CancelParkingSessionCommand.Reason));
    }

    [Fact]
    public void Validator_rejects_reason_longer_than_max_length()
    {
        var reason = new string('a', ParkingSession.CancelReasonMaxLength + 1);

        var result = validator.Validate(new CancelParkingSessionCommand(Guid.NewGuid(), reason, StaffId));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CancelParkingSessionCommand.Reason));
    }

    [Fact]
    public async Task Handler_cancels_active_session_and_records_audit_entry()
    {
        var session = NewActiveSession();
        var repository = new FakeRepository(session);
        var auditLog = new FakeAuditLog();
        var handler = new CancelParkingSessionCommandHandler(repository, new FakeLotAccess(allowed: true), auditLog, new FixedClock(), new FakeSlotDesignator(null));

        var result = await handler.Handle(
            new CancelParkingSessionCommand(session.Id, " Vehicle reversed, did not enter ", StaffId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Equal(ParkingSessionStatus.Cancelled, session.Status);
        Assert.Equal(StaffId, session.CancelledBy);
        Assert.Equal("Vehicle reversed, did not enter", session.CancelReason);
        Assert.Equal(Now, session.CancelledAt);
        Assert.Equal(1, repository.SaveCount);

        var entry = Assert.Single(auditLog.Entries);
        Assert.Equal(AuditActions.SessionCancelled, entry.Action);
        Assert.Equal(StaffId, entry.ActorUserId);
        Assert.Equal(LotId, entry.LotId);
        Assert.Equal(session.Id.ToString(), entry.TargetId);
        Assert.Equal(Now, entry.OccurredAt);
        Assert.Equal("Active", entry.Details!["fromStatus"]);
        Assert.Equal("Cancelled", entry.Details["toStatus"]);
        Assert.DoesNotContain(entry.Details.Values, value => value.Contains("51A", StringComparison.OrdinalIgnoreCase));
        Assert.False(entry.Details.ContainsKey("releasedSlotCode"));
    }

    [Fact]
    public async Task Handler_releases_the_slot_of_the_cancelled_session_and_audits_it()
    {
        var session = NewActiveSession();
        var slots = new FakeSlotDesignator("A-01");
        var auditLog = new FakeAuditLog();
        var handler = new CancelParkingSessionCommandHandler(new FakeRepository(session), new FakeLotAccess(allowed: true), auditLog, new FixedClock(), slots);

        var result = await handler.Handle(
            new CancelParkingSessionCommand(session.Id, "Vehicle did not enter", StaffId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal([session.Id], slots.ReleasedSessionIds);
        Assert.Equal("A-01", Assert.Single(auditLog.Entries).Details!["releasedSlotCode"]);
    }

    [Fact]
    public async Task Handler_returns_not_found_for_unknown_session()
    {
        var auditLog = new FakeAuditLog();
        var handler = new CancelParkingSessionCommandHandler(new FakeRepository(null), new FakeLotAccess(allowed: true), auditLog, new FixedClock(), new FakeSlotDesignator(null));

        var result = await handler.Handle(
            new CancelParkingSessionCommand(Guid.NewGuid(), "Vehicle did not enter", StaffId),
            TestContext.Current.CancellationToken);

        Assert.Equal(CancelParkingSessionErrors.SessionNotFound, result.Error);
        Assert.Empty(auditLog.Entries);
    }

    [Fact]
    public async Task Handler_rejects_session_that_is_not_active()
    {
        var session = NewActiveSession();
        session.Cancel(OtherStaffId, "first cancel", EntryTime.AddMinutes(1));
        var repository = new FakeRepository(session);
        var auditLog = new FakeAuditLog();
        var slots = new FakeSlotDesignator("A-01");
        var handler = new CancelParkingSessionCommandHandler(repository, new FakeLotAccess(allowed: true), auditLog, new FixedClock(), slots);

        var result = await handler.Handle(
            new CancelParkingSessionCommand(session.Id, "second cancel", StaffId),
            TestContext.Current.CancellationToken);

        Assert.Equal(CancelParkingSessionErrors.SessionNotActive, result.Error);
        Assert.Equal(OtherStaffId, session.CancelledBy);
        Assert.Empty(auditLog.Entries);
        Assert.Equal(0, repository.SaveCount);
        Assert.Empty(slots.ReleasedSessionIds);
    }

    [Fact]
    public async Task Handler_refuses_staff_not_assigned_to_the_session_lot_and_keeps_it_active()
    {
        var session = NewActiveSession();
        var repository = new FakeRepository(session);
        var auditLog = new FakeAuditLog();
        var handler = new CancelParkingSessionCommandHandler(repository, new FakeLotAccess(allowed: false), auditLog, new FixedClock(), new FakeSlotDesignator(null));

        var result = await handler.Handle(
            new CancelParkingSessionCommand(session.Id, "Vehicle did not enter", StaffId),
            TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
        Assert.Equal(ParkingSessionStatus.Active, session.Status);
        Assert.Empty(auditLog.Entries);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Handler_refuses_to_cancel_a_motorbike_session()
    {
        var session = ParkingSession.Open(Guid.NewGuid(), LotId, "59X1-234.56", VehicleType.Motorbike, EntryTime, "qr-bike", null, null, Guid.NewGuid());
        var repository = new FakeRepository(session);
        var auditLog = new FakeAuditLog();
        var slots = new FakeSlotDesignator(null);
        var handler = new CancelParkingSessionCommandHandler(repository, new FakeLotAccess(allowed: true), auditLog, new FixedClock(), slots);

        var result = await handler.Handle(
            new CancelParkingSessionCommand(session.Id, "Rider changed mind", StaffId),
            TestContext.Current.CancellationToken);

        Assert.Equal(CancelParkingSessionErrors.CancelNotAllowed, result.Error);
        Assert.Equal(ParkingSessionStatus.Active, session.Status);
        Assert.Empty(auditLog.Entries);
        Assert.Equal(0, repository.SaveCount);
        Assert.Empty(slots.ReleasedSessionIds);
    }

    private static ParkingSession NewActiveSession() =>
        ParkingSession.Open(Guid.NewGuid(), LotId, "51A-123.45", VehicleType.Car, EntryTime, "qr-token", null, null);

    private sealed class FakeRepository(ParkingSession? session) : IParkingSessionRepository
    {
        public int SaveCount { get; private set; }

        public Task<bool> HasOpenSessionAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<ParkingSession>> ListOpenByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ParkingSession?> FindOpenByQrTokenAsync(string qrToken, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<ParkingSession> Items, int TotalCount)> ListByLotAsync(
            Guid lotId, ParkingSessionStatus? status, int skip, int take, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ParkingSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(session?.Id == sessionId ? session : null);

        public void Add(ParkingSession added) => throw new NotSupportedException();

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(true);
        }
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

    private sealed class FakeSlotDesignator(string? heldSlotCode) : ISlotDesignator
    {
        public List<Guid> ReleasedSessionIds { get; } = [];

        public Task<DesignatedSlot?> DesignateCarSlotAsync(Guid lotId, Guid sessionId, DateTimeOffset assignedAt, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string?> ReleaseSessionSlotAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            ReleasedSessionIds.Add(sessionId);
            return Task.FromResult(heldSlotCode);
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
