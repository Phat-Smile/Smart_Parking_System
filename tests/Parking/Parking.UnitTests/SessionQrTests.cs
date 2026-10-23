using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Application.ParkingSessions.FindParkingSessionByQr;
using ParkingSystem.Parking.Application.ParkingSessions.ListMyParkingSessions;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class SessionQrTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid StaffId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset EntryTime = new(2026, 10, 7, 8, 0, 0, TimeSpan.Zero);
    private readonly FindParkingSessionByQrQueryValidator validator = new();

    [Fact]
    public async Task My_sessions_returns_qr_of_the_callers_open_sessions()
    {
        var session = NewSession("qr-1", UserId);
        var repository = new FakeRepository(session) { OpenForUser = [session] };
        var handler = new ListMyParkingSessionsQueryHandler(repository, new FakeZoneLabels());

        var result = await handler.Handle(new ListMyParkingSessionsQuery(UserId), TestContext.Current.CancellationToken);

        var item = Assert.Single(result);
        Assert.Equal(session.Id, item.SessionId);
        Assert.Equal("qr-1", item.QrToken);
        Assert.Equal("Active", item.Status);
        Assert.Equal(UserId, repository.LastUserId);
        // A car session has no motorbike zone to show.
        Assert.Null(item.ZoneCode);
    }

    [Fact]
    public async Task My_sessions_shows_the_zone_and_floor_of_a_motorbike_session()
    {
        var session = ParkingSession.Open(Guid.NewGuid(), LotId, "59X1-234.56", VehicleType.Motorbike, EntryTime, "qr-bike", UserId, Guid.NewGuid(), FakeZoneLabels.ZoneId);
        var handler = new ListMyParkingSessionsQueryHandler(new FakeRepository(session) { OpenForUser = [session] }, new FakeZoneLabels());

        var item = Assert.Single(await handler.Handle(new ListMyParkingSessionsQuery(UserId), TestContext.Current.CancellationToken));

        Assert.Equal("M2", item.ZoneCode);
        Assert.Equal("Tầng 2", item.FloorName);
    }

    [Fact]
    public async Task My_sessions_is_empty_when_the_account_has_no_open_session()
    {
        var handler = new ListMyParkingSessionsQueryHandler(new FakeRepository(null), new FakeZoneLabels());

        var result = await handler.Handle(new ListMyParkingSessionsQuery(UserId), TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Find_by_qr_returns_the_open_session()
    {
        var session = NewSession("qr-1", UserId);
        var handler = new FindParkingSessionByQrQueryHandler(new FakeRepository(session), new FakeLotAccess(allowed: true));

        var result = await handler.Handle(new FindParkingSessionByQrQuery("qr-1", StaffId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(session.Id, result.Value!.SessionId);
        Assert.Equal("51A-123.45", result.Value.PlateNumber);
        Assert.Equal("RegisteredWalkIn", result.Value.CustomerType);
        Assert.Equal("Active", result.Value.Status);
    }

    [Fact]
    public async Task Find_by_qr_rejects_unknown_qr()
    {
        var handler = new FindParkingSessionByQrQueryHandler(new FakeRepository(null), new FakeLotAccess(allowed: true));

        var result = await handler.Handle(new FindParkingSessionByQrQuery("unknown", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(FindParkingSessionByQrErrors.QrInvalid, result.Error);
    }

    [Fact]
    public async Task Find_by_qr_refuses_staff_not_assigned_to_the_session_lot()
    {
        var handler = new FindParkingSessionByQrQueryHandler(new FakeRepository(NewSession("qr-1", UserId)), new FakeLotAccess(allowed: false));

        var result = await handler.Handle(new FindParkingSessionByQrQuery("qr-1", StaffId), TestContext.Current.CancellationToken);

        Assert.Equal(ParkingLotAccessErrors.AccessDenied, result.Error);
    }

    [Fact]
    public void Validator_requires_the_caller()
    {
        Assert.False(validator.Validate(new FindParkingSessionByQrQuery("qr-1", Guid.Empty)).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_requires_a_qr_token(string qrToken)
    {
        Assert.False(validator.Validate(new FindParkingSessionByQrQuery(qrToken, StaffId)).IsValid);
    }

    [Fact]
    public void Validator_rejects_qr_token_longer_than_max_length()
    {
        var qrToken = new string('a', ParkingSession.QrTokenMaxLength + 1);

        Assert.False(validator.Validate(new FindParkingSessionByQrQuery(qrToken, StaffId)).IsValid);
    }

    private static ParkingSession NewSession(string qrToken, Guid? userId) =>
        ParkingSession.Open(Guid.NewGuid(), LotId, "51A-123.45", VehicleType.Car, EntryTime, qrToken, userId, userId is null ? null : Guid.NewGuid());

    private sealed class FakeZoneLabels : IZoneLabelReader
    {
        public static readonly Guid ZoneId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        public Task<IReadOnlyDictionary<Guid, ZoneLabel>> GetLabelsAsync(IReadOnlyCollection<Guid> zoneIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, ZoneLabel>>(zoneIds.Where(id => id == ZoneId).ToDictionary(id => id, _ => new ZoneLabel("M2", "Tầng 2")));
    }

    private sealed class FakeLotAccess(bool allowed) : IParkingLotAccess
    {
        public Task<bool> CanOperateAsync(Guid accountId, Guid lotId, CancellationToken cancellationToken) => Task.FromResult(allowed);
    }

    private sealed class FakeRepository(ParkingSession? session) : IParkingSessionRepository
    {
        public IReadOnlyList<ParkingSession> OpenForUser { get; init; } = [];
        public Guid? LastUserId { get; private set; }

        public Task<bool> HasOpenSessionAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ParkingSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ParkingSession>> ListOpenByUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            LastUserId = userId;
            return Task.FromResult(OpenForUser);
        }

        public Task<ParkingSession?> FindOpenByQrTokenAsync(string qrToken, CancellationToken cancellationToken) =>
            Task.FromResult(session?.QrToken == qrToken ? session : null);

        public void Add(ParkingSession added) => throw new NotSupportedException();

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
