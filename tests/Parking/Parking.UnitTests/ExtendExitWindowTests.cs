using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Application.ParkingSessions.ExtendExitWindow;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

// Q-EXIT-WINDOW (BR-09.d): staff grant a one-time 15-minute extension for a Paid session with mandatory reason.
public sealed class ExtendExitWindowTests
{
    private const string Plate = "51A-123.45";
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid StaffId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset EntryTime = new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PaidAt = EntryTime.AddHours(2);
    private static readonly DateTimeOffset Now = PaidAt.AddMinutes(14);
    private readonly ExtendExitWindowCommandValidator validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_requires_a_reason(string reason)
    {
        var result = validator.Validate(new ExtendExitWindowCommand(Guid.NewGuid(), reason, StaffId));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ExtendExitWindowCommand.Reason));
    }

    [Fact]
    public void Validator_rejects_reason_longer_than_max_length()
    {
        var reason = new string('x', ParkingSession.StaffActionReasonMaxLength + 1);

        var result = validator.Validate(new ExtendExitWindowCommand(Guid.NewGuid(), reason, StaffId));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ExtendExitWindowCommand.Reason));
    }

    [Fact]
    public void Validator_requires_session_id_and_actor()
    {
        var result = validator.Validate(new ExtendExitWindowCommand(Guid.Empty, "Traffic jam at exit", Guid.Empty));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ExtendExitWindowCommand.SessionId));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ExtendExitWindowCommand.ActorUserId));
    }

    [Fact]
    public async Task Handler_extends_paid_session_exit_window_and_records_audit_log()
    {
        var session = NewPaidSession();
        var repository = new FakeRepository(session);
        var auditLog = new FakeAuditLog();
        var handler = new ExtendExitWindowCommandHandler(repository, new FakeLotAccess(allowed: true), auditLog, new FixedClock());

        var result = await handler.Handle(
            new ExtendExitWindowCommand(session.Id, "  Exit gate queue due to barrier motor glitch  ", StaffId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(session.HasExitWindowExtension);
        Assert.Equal(Now, session.ExitWindowExtendedAt);
        Assert.Equal("Exit gate queue due to barrier motor glitch", session.ExitWindowExtensionReason);
        Assert.Equal(PaidAt.AddMinutes(30), session.ExitWindowEndsAt);
        Assert.Equal(session.ExitWindowEndsAt, result.Value!.NewExitWindowEndsAt);
        Assert.Equal(1, repository.SaveCount);

        var entry = Assert.Single(auditLog.Entries);
        Assert.Equal(AuditActions.SessionExitWindowExtended, entry.Action);
        Assert.Equal(StaffId, entry.ActorUserId);
        Assert.Equal(LotId, entry.LotId);
        Assert.Equal(nameof(ParkingSession), entry.TargetType);
        Assert.Equal(session.Id.ToString(), entry.TargetId);
        Assert.Equal("Exit gate queue due to barrier motor glitch", entry.Reason);
        Assert.Equal(Now, entry.OccurredAt);
        Assert.NotNull(entry.Details);
        Assert.Equal(PaidAt.AddMinutes(30).ToString("o"), entry.Details["newExitWindowEndsAt"]);
    }

    [Fact]
    public async Task Handler_returns_not_found_when_session_does_not_exist()
    {
        var repository = new FakeRepository(null);
        var handler = new ExtendExitWindowCommandHandler(repository, new FakeLotAccess(allowed: true), new FakeAuditLog(), new FixedClock());

        var result = await handler.Handle(
            new ExtendExitWindowCommand(Guid.NewGuid(), "Valid reason", StaffId),
            TestContext.Current.CancellationToken);

        Assert.Equal(ExtendExitWindowErrors.SessionNotFound, result.Error);
    }

    [Fact]
    public async Task Handler_returns_access_denied_when_caller_cannot_operate_lot()
    {
        var session = NewPaidSession();
        var repository = new FakeRepository(session);
        var auditLog = new FakeAuditLog();
        var handler = new ExtendExitWindowCommandHandler(repository, new FakeLotAccess(allowed: false), auditLog, new FixedClock());

        var result = await handler.Handle(
            new ExtendExitWindowCommand(session.Id, "Valid reason", StaffId),
            TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
        Assert.False(session.HasExitWindowExtension);
        Assert.Empty(auditLog.Entries);
    }

    [Theory]
    [InlineData(ParkingSessionStatus.Active)]
    [InlineData(ParkingSessionStatus.Completed)]
    [InlineData(ParkingSessionStatus.Cancelled)]
    public async Task Handler_rejects_extension_for_non_paid_session(ParkingSessionStatus status)
    {
        var session = NewSession(status);
        var repository = new FakeRepository(session);
        var handler = new ExtendExitWindowCommandHandler(repository, new FakeLotAccess(allowed: true), new FakeAuditLog(), new FixedClock());

        var result = await handler.Handle(
            new ExtendExitWindowCommand(session.Id, "Valid reason", StaffId),
            TestContext.Current.CancellationToken);

        Assert.Equal(ExtendExitWindowErrors.CannotExtend, result.Error);
        Assert.False(session.HasExitWindowExtension);
    }

    [Fact]
    public async Task Handler_rejects_second_extension_attempt()
    {
        var session = NewPaidSession();
        session.ExtendExitWindow(Now.AddMinutes(-5), "First extension");
        var repository = new FakeRepository(session);
        var handler = new ExtendExitWindowCommandHandler(repository, new FakeLotAccess(allowed: true), new FakeAuditLog(), new FixedClock());

        var result = await handler.Handle(
            new ExtendExitWindowCommand(session.Id, "Second extension attempt", StaffId),
            TestContext.Current.CancellationToken);

        Assert.Equal(ExtendExitWindowErrors.CannotExtend, result.Error);
        Assert.Equal("First extension", session.ExitWindowExtensionReason);
    }

    [Fact]
    public void RecordExitArrival_records_first_arrival_and_ignores_subsequent_calls()
    {
        var session = NewPaidSession();
        var firstArrival = PaidAt.AddMinutes(10);
        var secondArrival = PaidAt.AddMinutes(20);

        var firstResult = session.RecordExitArrival(firstArrival);
        var secondResult = session.RecordExitArrival(secondArrival);

        Assert.True(firstResult);
        Assert.False(secondResult);
        Assert.Equal(firstArrival, session.ExitArrivedAt);
    }

    [Fact]
    public void Exit_arrived_within_window_remains_valid_even_if_checked_later()
    {
        var session = NewPaidSession();
        var arrival = PaidAt.AddMinutes(12);
        session.RecordExitArrival(arrival);

        // Checked at T + 25m, but vehicle arrived at T + 12m (within 15m window).
        Assert.False(session.IsExitWindowOver(PaidAt.AddMinutes(25)));
        Assert.True(ExitEligibility.CanExit(session, plateMatches: true, qrMatches: true, PaidAt.AddMinutes(25)));
    }

    [Fact]
    public void Extension_allows_exit_up_to_30_minutes_after_payment()
    {
        var session = NewPaidSession();
        session.ExtendExitWindow(Now, "Staff extended due to queue");

        // 20 minutes after payment: without extension this would be late, but with extension it is in time.
        Assert.False(session.IsExitWindowOver(PaidAt.AddMinutes(20)));
        Assert.True(ExitEligibility.CanExit(session, plateMatches: true, qrMatches: true, PaidAt.AddMinutes(20)));

        // 30 minutes and 1 second after payment: late.
        Assert.True(session.IsExitWindowOver(PaidAt.AddMinutes(30).AddSeconds(1)));
        Assert.False(ExitEligibility.CanExit(session, plateMatches: true, qrMatches: true, PaidAt.AddMinutes(30).AddSeconds(1)));
    }

    private static ParkingSession NewPaidSession()
    {
        var session = ParkingSession.Open(Guid.NewGuid(), LotId, Plate, VehicleType.Car, EntryTime, "qr-paid", null, null);
        session.MarkPaid(PaidAt);
        return session;
    }

    private static ParkingSession NewSession(ParkingSessionStatus status)
    {
        var session = ParkingSession.Open(Guid.NewGuid(), LotId, Plate, VehicleType.Car, EntryTime, "qr-status", null, null);
        if (status == ParkingSessionStatus.Cancelled)
            session.Cancel(StaffId, "did not enter", EntryTime);
        else if (status != ParkingSessionStatus.Active)
            typeof(ParkingSession).GetProperty(nameof(ParkingSession.Status))!.SetValue(session, status);
        return session;
    }

    private sealed class FakeRepository(ParkingSession? session) : IParkingSessionRepository
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

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
