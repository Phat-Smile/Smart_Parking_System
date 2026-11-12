using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Application.ParkingSessions.FindOpenParkingSessionByPlate;
using ParkingSystem.Parking.Application.ParkingSessions.ReissueParkingSessionQr;
using ParkingSystem.Parking.Application.ParkingSessions.VerifyParkingSessionExit;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

// US-STAFF-004: find the open session by plate, reissue a lost QR and record a manual exit check (BR-11.c, BR-11.d).
public sealed class ExitVerificationTests
{
    private const string Plate = "51A-123.45";
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid StaffId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset EntryTime = new(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = EntryTime.AddHours(2);

    [Theory]
    [InlineData(ParkingSessionStatus.Active, true, true, false)]
    [InlineData(ParkingSessionStatus.Paid, true, true, true)]
    [InlineData(ParkingSessionStatus.Paid, true, false, false)]
    [InlineData(ParkingSessionStatus.Paid, false, true, false)]
    [InlineData(ParkingSessionStatus.Completed, true, true, false)]
    [InlineData(ParkingSessionStatus.Cancelled, true, true, false)]
    public void Exit_needs_plate_match_qr_match_and_payment(ParkingSessionStatus status, bool plateMatches, bool qrMatches, bool expected)
    {
        var session = NewSession(status);

        Assert.Equal(expected, ExitEligibility.CanExit(session, plateMatches, qrMatches, Now));
    }

    [Fact]
    public async Task Find_by_plate_returns_the_open_session_without_its_qr()
    {
        var session = NewSession(ParkingSessionStatus.Active);
        var repository = new FakeRepository(session);
        var handler = new FindOpenParkingSessionByPlateQueryHandler(repository, new FakeLotAccess(allowed: true));

        var result = await handler.Handle(new FindOpenParkingSessionByPlateQuery(LotId, " 51a-123.45 ", StaffId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(session.Id, result.Value!.SessionId);
        Assert.Equal(Plate, result.Value.PlateNumber);
        Assert.Equal("Active", result.Value.Status);
        // The plate is normalized the same way as at entry.
        Assert.Equal((LotId, Plate), repository.LastPlateLookup);
    }

    [Fact]
    public async Task Find_by_plate_returns_not_found_when_the_plate_has_no_open_session()
    {
        var handler = new FindOpenParkingSessionByPlateQueryHandler(new FakeRepository(null), new FakeLotAccess(allowed: true));

        var result = await handler.Handle(new FindOpenParkingSessionByPlateQuery(LotId, Plate, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(FindOpenParkingSessionByPlateErrors.SessionNotFound, result.Error);
    }

    [Fact]
    public async Task Find_by_plate_checks_lot_access_before_reading_sessions()
    {
        var repository = new FakeRepository(NewSession(ParkingSessionStatus.Active));
        var handler = new FindOpenParkingSessionByPlateQueryHandler(repository, new FakeLotAccess(allowed: false));

        var result = await handler.Handle(new FindOpenParkingSessionByPlateQuery(LotId, Plate, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
        Assert.Null(repository.LastPlateLookup);
    }

    [Fact]
    public void Find_by_plate_validator_requires_lot_and_plate()
    {
        var validator = new FindOpenParkingSessionByPlateQueryValidator();

        Assert.False(validator.Validate(new FindOpenParkingSessionByPlateQuery(Guid.Empty, Plate, StaffId)).IsValid);
        Assert.False(validator.Validate(new FindOpenParkingSessionByPlateQuery(LotId, "  ", StaffId)).IsValid);
        Assert.False(validator.Validate(new FindOpenParkingSessionByPlateQuery(LotId, new string('A', ParkingSession.PlateNumberMaxLength + 1), StaffId)).IsValid);
    }

    [Fact]
    public async Task Reissue_replaces_the_qr_and_records_an_audit_entry_without_the_token()
    {
        var session = NewSession(ParkingSessionStatus.Active);
        var repository = new FakeRepository(session);
        var auditLog = new FakeAuditLog();
        var handler = new ReissueParkingSessionQrCommandHandler(repository, new FakeLotAccess(allowed: true), auditLog, new FixedClock());

        var result = await handler.Handle(
            new ReissueParkingSessionQrCommand(session.Id, " Customer lost the QR ", StaffId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotEqual("old-qr", session.QrToken);
        Assert.Equal(session.QrToken, result.Value!.QrToken);
        Assert.Equal(Now, result.Value.ReissuedAt);
        Assert.Equal(1, repository.SaveCount);

        var entry = Assert.Single(auditLog.Entries);
        Assert.Equal(AuditActions.SessionQrReissued, entry.Action);
        Assert.Equal(StaffId, entry.ActorUserId);
        Assert.Equal(LotId, entry.LotId);
        Assert.Equal(session.Id.ToString(), entry.TargetId);
        Assert.Equal("Customer lost the QR", entry.Reason);
        Assert.Null(entry.Details);
    }

    [Fact]
    public async Task Reissue_gives_a_different_qr_each_time()
    {
        var session = NewSession(ParkingSessionStatus.Active);
        var handler = new ReissueParkingSessionQrCommandHandler(new FakeRepository(session), new FakeLotAccess(allowed: true), new FakeAuditLog(), new FixedClock());

        var first = await handler.Handle(new ReissueParkingSessionQrCommand(session.Id, "lost", StaffId), TestContext.Current.CancellationToken);
        var second = await handler.Handle(new ReissueParkingSessionQrCommand(session.Id, "lost again", StaffId), TestContext.Current.CancellationToken);

        Assert.NotEqual(first.Value!.QrToken, second.Value!.QrToken);
        Assert.Equal(second.Value.QrToken, session.QrToken);
    }

    [Theory]
    [InlineData(ParkingSessionStatus.Paid)]
    [InlineData(ParkingSessionStatus.Completed)]
    [InlineData(ParkingSessionStatus.Cancelled)]
    public async Task Reissue_is_refused_unless_the_session_is_active(ParkingSessionStatus status)
    {
        var session = NewSession(status);
        var repository = new FakeRepository(session);
        var auditLog = new FakeAuditLog();
        var handler = new ReissueParkingSessionQrCommandHandler(repository, new FakeLotAccess(allowed: true), auditLog, new FixedClock());

        var result = await handler.Handle(new ReissueParkingSessionQrCommand(session.Id, "lost", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ReissueParkingSessionQrErrors.SessionNotActive, result.Error);
        Assert.Equal("old-qr", session.QrToken);
        Assert.Empty(auditLog.Entries);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Reissue_refuses_staff_not_assigned_to_the_session_lot()
    {
        var session = NewSession(ParkingSessionStatus.Active);
        var repository = new FakeRepository(session);
        var handler = new ReissueParkingSessionQrCommandHandler(repository, new FakeLotAccess(allowed: false), new FakeAuditLog(), new FixedClock());

        var result = await handler.Handle(new ReissueParkingSessionQrCommand(session.Id, "lost", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
        Assert.Equal("old-qr", session.QrToken);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Reissue_returns_not_found_for_unknown_session()
    {
        var handler = new ReissueParkingSessionQrCommandHandler(new FakeRepository(null), new FakeLotAccess(allowed: true), new FakeAuditLog(), new FixedClock());

        var result = await handler.Handle(new ReissueParkingSessionQrCommand(Guid.NewGuid(), "lost", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ReissueParkingSessionQrErrors.SessionNotFound, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reissue_and_manual_verify_require_a_reason(string reason)
    {
        Assert.False(new ReissueParkingSessionQrCommandValidator().Validate(new ReissueParkingSessionQrCommand(Guid.NewGuid(), reason, StaffId)).IsValid);
        Assert.False(new VerifyParkingSessionExitCommandValidator().Validate(new VerifyParkingSessionExitCommand(Guid.NewGuid(), reason, StaffId)).IsValid);
    }

    [Fact]
    public void Reissue_and_manual_verify_reject_a_reason_longer_than_the_audit_column()
    {
        var reason = new string('a', ParkingSession.StaffActionReasonMaxLength + 1);

        Assert.False(new ReissueParkingSessionQrCommandValidator().Validate(new ReissueParkingSessionQrCommand(Guid.NewGuid(), reason, StaffId)).IsValid);
        Assert.False(new VerifyParkingSessionExitCommandValidator().Validate(new VerifyParkingSessionExitCommand(Guid.NewGuid(), reason, StaffId)).IsValid);
    }

    [Fact]
    public async Task Manual_verify_keeps_the_barrier_closed_while_money_is_still_due()
    {
        var session = NewSession(ParkingSessionStatus.Active);
        var repository = new FakeRepository(session);
        var auditLog = new FakeAuditLog();
        var handler = new VerifyParkingSessionExitCommandHandler(repository, new FakeLotAccess(allowed: true), auditLog, new FixedClock());

        var result = await handler.Handle(
            new VerifyParkingSessionExitCommand(session.Id, " Plate misread at the exit gate ", StaffId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.CanExit);
        Assert.Equal("Active", result.Value.Status);
        Assert.Equal(Now, result.Value.VerifiedAt);
        // Audit only: the session itself does not change.
        Assert.Equal(ParkingSessionStatus.Active, session.Status);
        Assert.Equal(1, repository.SaveCount);

        var entry = Assert.Single(auditLog.Entries);
        Assert.Equal(AuditActions.SessionExitManuallyVerified, entry.Action);
        Assert.Equal(StaffId, entry.ActorUserId);
        Assert.Equal(LotId, entry.LotId);
        Assert.Equal("Plate misread at the exit gate", entry.Reason);
        Assert.Equal("Active", entry.Details!["status"]);
        Assert.Equal("False", entry.Details["canExit"]);
        Assert.DoesNotContain(entry.Details.Values, value => value.Contains("51A", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Manual_verify_lets_a_paid_session_leave()
    {
        var session = NewSession(ParkingSessionStatus.Paid);
        var handler = new VerifyParkingSessionExitCommandHandler(new FakeRepository(session), new FakeLotAccess(allowed: true), new FakeAuditLog(), new FixedClock());

        var result = await handler.Handle(new VerifyParkingSessionExitCommand(session.Id, "QR lost, checked in person", StaffId), TestContext.Current.CancellationToken);

        Assert.True(result.Value!.CanExit);
        Assert.Equal("Paid", result.Value.Status);
    }

    [Theory]
    [InlineData(ParkingSessionStatus.Completed)]
    [InlineData(ParkingSessionStatus.Cancelled)]
    public async Task Manual_verify_is_refused_for_a_closed_session(ParkingSessionStatus status)
    {
        var repository = new FakeRepository(NewSession(status));
        var auditLog = new FakeAuditLog();
        var handler = new VerifyParkingSessionExitCommandHandler(repository, new FakeLotAccess(allowed: true), auditLog, new FixedClock());

        var result = await handler.Handle(new VerifyParkingSessionExitCommand(repository.Session!.Id, "checked", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(VerifyParkingSessionExitErrors.SessionNotOpen, result.Error);
        Assert.Empty(auditLog.Entries);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Manual_verify_refuses_staff_not_assigned_to_the_session_lot()
    {
        var repository = new FakeRepository(NewSession(ParkingSessionStatus.Paid));
        var auditLog = new FakeAuditLog();
        var handler = new VerifyParkingSessionExitCommandHandler(repository, new FakeLotAccess(allowed: false), auditLog, new FixedClock());

        var result = await handler.Handle(new VerifyParkingSessionExitCommand(repository.Session!.Id, "checked", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
        Assert.Empty(auditLog.Entries);
    }

    [Fact]
    public async Task Manual_verify_returns_not_found_for_unknown_session()
    {
        var handler = new VerifyParkingSessionExitCommandHandler(new FakeRepository(null), new FakeLotAccess(allowed: true), new FakeAuditLog(), new FixedClock());

        var result = await handler.Handle(new VerifyParkingSessionExitCommand(Guid.NewGuid(), "checked", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(VerifyParkingSessionExitErrors.SessionNotFound, result.Error);
    }

    // No domain method sets Paid or Completed yet (Payment contract #6, US-PAY-006), so the status is set directly here.
    private static ParkingSession NewSession(ParkingSessionStatus status)
    {
        var session = ParkingSession.Open(Guid.NewGuid(), LotId, Plate, VehicleType.Car, EntryTime, "old-qr", null, null);
        if (status == ParkingSessionStatus.Cancelled)
            session.Cancel(StaffId, "did not enter", EntryTime);
        else if (status != ParkingSessionStatus.Active)
            typeof(ParkingSession).GetProperty(nameof(ParkingSession.Status))!.SetValue(session, status);
        return session;
    }

    private sealed class FakeRepository(ParkingSession? session) : IParkingSessionRepository
    {
        public ParkingSession? Session => session;
        public int SaveCount { get; private set; }
        public (Guid LotId, string Plate)? LastPlateLookup { get; private set; }

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

        public Task<ParkingSession?> FindOpenByPlateAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken)
        {
            LastPlateLookup = (lotId, normalizedPlate);
            return Task.FromResult(session?.LotId == lotId && session.PlateNumber == normalizedPlate ? session : null);
        }

        public Task<ParkingSession?> FindByIdForUpdateAsync(Guid sessionId, CancellationToken cancellationToken) => FindByIdAsync(sessionId, cancellationToken);

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

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
