using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.AssignLotMember;
using ParkingSystem.Parking.Application.ParkingLots.ListLotAssignments;
using ParkingSystem.Parking.Application.ParkingLots.ListStaffLots;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Application.ParkingSessions.GetParkingSession;
using ParkingSystem.Parking.Application.ParkingSessions.ListLotParkingSessions;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

// Lead task S2 (2026-10-27): the 4 read APIs for staff, admin and the customer QR page.
public sealed class StaffSessionQueriesTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid StaffId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset EntryTime = new(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);
    private readonly ListLotParkingSessionsQueryValidator validator = new();

    [Fact]
    public async Task Session_owner_gets_the_detail_with_the_qr()
    {
        var session = NewSession(CustomerId);
        var handler = new GetParkingSessionQueryHandler(new FakeSessions(session), new FakeLotAccess(allowed: false), new FakeSlotCodes(session.Id, "A-01"));

        var result = await handler.Handle(new GetParkingSessionQuery(session.Id, CustomerId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var detail = result.Value!;
        Assert.Equal(session.Id, detail.SessionId);
        Assert.Equal(LotId, detail.LotId);
        Assert.Equal("51A-123.45", detail.PlateNumber);
        Assert.Equal("Car", detail.VehicleType);
        Assert.Equal("Active", detail.Status);
        Assert.Equal(EntryTime, detail.EntryTime);
        Assert.Equal("qr-1", detail.QrToken);
        // US-PARK-007: the slot the car was given.
        Assert.Equal("A-01", detail.SlotCode);
        // Decision 2026-10-27: no payment data yet in S2.
        Assert.Null(detail.PaidAtUtc);
        Assert.Null(detail.GraceUntilUtc);
    }

    [Fact]
    public async Task Staff_assigned_to_the_lot_get_the_detail_without_the_qr()
    {
        var session = NewSession(CustomerId);
        var handler = new GetParkingSessionQueryHandler(new FakeSessions(session), new FakeLotAccess(allowed: true), new FakeSlotCodes());

        var result = await handler.Handle(new GetParkingSessionQuery(session.Id, StaffId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.QrToken);
    }

    [Fact]
    public async Task Owner_of_a_closed_session_gets_no_qr()
    {
        var session = NewSession(CustomerId);
        session.Cancel(StaffId, "Vehicle did not enter", EntryTime.AddMinutes(1));
        var handler = new GetParkingSessionQueryHandler(new FakeSessions(session), new FakeLotAccess(allowed: false), new FakeSlotCodes());

        var result = await handler.Handle(new GetParkingSessionQuery(session.Id, CustomerId), TestContext.Current.CancellationToken);

        Assert.Equal("Cancelled", result.Value!.Status);
        Assert.Null(result.Value.QrToken);
    }

    [Fact]
    public async Task Unknown_session_is_not_found()
    {
        var handler = new GetParkingSessionQueryHandler(new FakeSessions(null), new FakeLotAccess(allowed: true), new FakeSlotCodes());

        var result = await handler.Handle(new GetParkingSessionQuery(Guid.NewGuid(), StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(GetParkingSessionErrors.SessionNotFound, result.Error);
    }

    [Fact]
    public async Task Another_customer_or_unassigned_staff_get_access_denied()
    {
        var session = NewSession(CustomerId);
        var handler = new GetParkingSessionQueryHandler(new FakeSessions(session), new FakeLotAccess(allowed: false), new FakeSlotCodes());

        var result = await handler.Handle(new GetParkingSessionQuery(session.Id, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
    }

    [Fact]
    public async Task Session_without_an_account_is_visible_only_to_assigned_staff()
    {
        var session = NewSession(userId: null);
        var handler = new GetParkingSessionQueryHandler(new FakeSessions(session), new FakeLotAccess(allowed: false), new FakeSlotCodes());

        var result = await handler.Handle(new GetParkingSessionQuery(session.Id, CustomerId), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
    }

    [Fact]
    public async Task Lot_sessions_are_refused_to_staff_not_assigned_to_the_lot()
    {
        var sessions = new FakeSessions(null);
        var handler = new ListLotParkingSessionsQueryHandler(sessions, new FakeLotAccess(allowed: false), new FakeSlotCodes());

        var result = await handler.Handle(new ListLotParkingSessionsQuery(LotId, null, 1, 20, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
        Assert.Null(sessions.LastPage);
    }

    [Fact]
    public async Task Lot_sessions_tell_registered_and_unregistered_walk_ins_apart()
    {
        var registered = NewSession(CustomerId);
        var unregistered = NewSession(userId: null);
        var sessions = new FakeSessions(registered) { LotPage = ([registered, unregistered], 2) };
        var handler = new ListLotParkingSessionsQueryHandler(sessions, new FakeLotAccess(allowed: true), new FakeSlotCodes());

        var result = await handler.Handle(new ListLotParkingSessionsQuery(LotId, null, 1, 20, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["RegisteredWalkIn", "UnregisteredWalkIn"],
            result.Value!.Items.Select(item => item.CustomerType).ToArray());
    }

    [Fact]
    public async Task Lot_sessions_ask_for_the_requested_page_and_status()
    {
        var session = NewSession(CustomerId);
        var sessions = new FakeSessions(session) { LotPage = ([session], 41) };
        var handler = new ListLotParkingSessionsQueryHandler(sessions, new FakeLotAccess(allowed: true), new FakeSlotCodes(session.Id, "A-02"));

        var result = await handler.Handle(new ListLotParkingSessionsQuery(LotId, "active", 3, 20, StaffId), TestContext.Current.CancellationToken);

        Assert.Equal((LotId, ParkingSessionStatus.Active, 40, 20), sessions.LastPage);
        var page = result.Value!;
        Assert.Equal(41, page.TotalCount);
        Assert.Equal(3, page.Page);
        Assert.Equal(20, page.PageSize);
        var item = Assert.Single(page.Items);
        Assert.Equal(session.Id, item.SessionId);
        Assert.Equal("Active", item.Status);
        Assert.Equal("A-02", item.SlotCode);
    }

    [Fact]
    public async Task Lot_sessions_without_status_ask_for_every_status()
    {
        var sessions = new FakeSessions(null);
        var handler = new ListLotParkingSessionsQueryHandler(sessions, new FakeLotAccess(allowed: true), new FakeSlotCodes());

        await handler.Handle(new ListLotParkingSessionsQuery(LotId, null, 1, 20, StaffId), TestContext.Current.CancellationToken);

        Assert.Null(sessions.LastPage!.Value.Status);
    }

    [Theory]
    [InlineData(null, 1, 20)]
    [InlineData("Active", 1, 1)]
    [InlineData("paid", 2, LotParkingSessionPaging.MaxPageSize)]
    public void Validator_accepts_valid_paging_and_status(string? status, int page, int pageSize)
    {
        Assert.True(validator.Validate(new ListLotParkingSessionsQuery(LotId, status, page, pageSize, StaffId)).IsValid);
    }

    [Theory]
    [InlineData(null, 0, 20)]
    [InlineData(null, 1, 0)]
    [InlineData(null, 1, LotParkingSessionPaging.MaxPageSize + 1)]
    [InlineData("Parked", 1, 20)]
    [InlineData("1", 1, 20)]
    public void Validator_rejects_bad_paging_or_status(string? status, int page, int pageSize)
    {
        Assert.False(validator.Validate(new ListLotParkingSessionsQuery(LotId, status, page, pageSize, StaffId)).IsValid);
    }

    [Fact]
    public async Task Assignments_of_an_unknown_lot_are_not_found()
    {
        var handler = new ListLotAssignmentsQueryHandler(new FakeAssignments(lotExists: false));

        var result = await handler.Handle(new ListLotAssignmentsQuery(LotId), TestContext.Current.CancellationToken);

        Assert.Equal(LotMemberErrors.LotNotFound, result.Error);
    }

    [Fact]
    public async Task Assignments_list_account_role_and_assigned_time()
    {
        var assignment = ParkingLotAssignment.Create(LotId, StaffId, ParkingLotMemberRole.Staff, Guid.NewGuid(), EntryTime);
        var handler = new ListLotAssignmentsQueryHandler(new FakeAssignments(lotExists: true) { Members = [assignment] });

        var result = await handler.Handle(new ListLotAssignmentsQuery(LotId), TestContext.Current.CancellationToken);

        var item = Assert.Single(result.Value!);
        Assert.Equal(new LotAssignment(StaffId, "Staff", EntryTime), item);
    }

    [Fact]
    public async Task Staff_lots_are_the_approved_lots_of_the_caller()
    {
        var lot = new StaffLot(LotId, "Central", "1 Demo Street", "Outdoor");
        var assignments = new FakeAssignments(lotExists: true) { Lots = [lot] };
        var handler = new ListStaffLotsQueryHandler(assignments);

        var result = await handler.Handle(new ListStaffLotsQuery(StaffId), TestContext.Current.CancellationToken);

        Assert.Equal([lot], result);
        Assert.Equal(StaffId, assignments.LastAccountId);
    }

    private static ParkingSession NewSession(Guid? userId) =>
        ParkingSession.Open(Guid.NewGuid(), LotId, "51A-123.45", VehicleType.Car, EntryTime, "qr-1", userId, userId is null ? null : Guid.NewGuid());

    private sealed class FakeSlotCodes(Guid? sessionId = null, string? slotCode = null) : ISlotDesignator
    {
        public Task<IReadOnlyDictionary<Guid, string>> FindSlotCodesAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(
                sessionId is { } id && slotCode is not null && sessionIds.Contains(id)
                    ? new Dictionary<Guid, string> { [id] = slotCode }
                    : new Dictionary<Guid, string>());

        public Task<int> LockLotAndCountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DesignatedSlot?> DesignateCarSlotAsync(Guid lotId, Guid sessionId, DateTimeOffset assignedAt, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string?> ReleaseSessionSlotAsync(Guid sessionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeLotAccess(bool allowed) : IParkingLotAccess
    {
        public Task<bool> CanOperateAsync(Guid accountId, Guid lotId, CancellationToken cancellationToken) => Task.FromResult(allowed);
    }

    private sealed class FakeSessions(ParkingSession? session) : IParkingSessionRepository
    {
        public (IReadOnlyList<ParkingSession> Items, int TotalCount) LotPage { get; init; } = ([], 0);
        public (Guid LotId, ParkingSessionStatus? Status, int Skip, int Take)? LastPage { get; private set; }

        public Task<ParkingSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(session?.Id == sessionId ? session : null);

        public Task<(IReadOnlyList<ParkingSession> Items, int TotalCount)> ListByLotAsync(
            Guid lotId, ParkingSessionStatus? status, int skip, int take, CancellationToken cancellationToken)
        {
            LastPage = (lotId, status, skip, take);
            return Task.FromResult(LotPage);
        }

        public Task<bool> HasOpenSessionAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ParkingSession>> ListOpenByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ParkingSession?> FindOpenByQrTokenAsync(string qrToken, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ParkingSession?> FindOpenByPlateAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Add(ParkingSession added) => throw new NotSupportedException();

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeAssignments(bool lotExists) : IParkingLotAssignmentRepository
    {
        public IReadOnlyList<ParkingLotAssignment> Members { get; init; } = [];
        public IReadOnlyList<StaffLot> Lots { get; init; } = [];
        public Guid? LastAccountId { get; private set; }

        public Task<bool> LotExistsAsync(Guid lotId, CancellationToken cancellationToken) => Task.FromResult(lotExists);

        public Task<IReadOnlyList<ParkingLotAssignment>> ListByLotAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult(Members);

        public Task<IReadOnlyList<StaffLot>> ListApprovedLotsOfMemberAsync(Guid accountId, CancellationToken cancellationToken)
        {
            LastAccountId = accountId;
            return Task.FromResult(Lots);
        }

        public Task<ParkingLotAssignment?> FindAsync(Guid lotId, Guid accountId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Add(ParkingLotAssignment assignment) => throw new NotSupportedException();

        public void Remove(ParkingLotAssignment assignment) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
