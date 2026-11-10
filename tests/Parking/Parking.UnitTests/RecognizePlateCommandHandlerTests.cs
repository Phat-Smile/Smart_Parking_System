using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Plate;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Application.ParkingSessions.RecognizePlate;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Parking.UnitTests;

public sealed class RecognizePlateCommandHandlerTests
{
    private static readonly Guid TestLotId = Guid.NewGuid();
    private static readonly Guid TestStaffId = Guid.NewGuid();

    [Fact]
    public async Task Handle_WithUnapprovedLot_ReturnsLotNotFound()
    {
        var handler = CreateHandler(
            lotApproved: false,
            canOperate: true);

        var command = new RecognizePlateCommand(TestLotId, "data:image/jpeg;base64,abc123456789", "Entry", TestStaffId);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RecognizePlateErrors.LotNotFound, result.Error);
    }

    [Fact]
    public async Task Handle_WithUnauthorizedActor_ReturnsLotAccessDenied()
    {
        var handler = CreateHandler(
            lotApproved: true,
            canOperate: false);

        var command = new RecognizePlateCommand(TestLotId, "data:image/jpeg;base64,abc123456789", "Entry", TestStaffId);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RecognizePlateErrors.LotAccessDenied, result.Error);
    }

    [Fact]
    public async Task Handle_WithEmptyImage_ReturnsUnreadablePlate()
    {
        var handler = CreateHandler(
            lotApproved: true,
            canOperate: true,
            ocrResult: new PlateOcrResult(string.Empty, string.Empty, 0.0, "Car"));

        var command = new RecognizePlateCommand(TestLotId, "data:image/jpeg;base64,empty", "Entry", TestStaffId);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RecognizePlateErrors.UnreadablePlate, result.Error);
    }

    [Fact]
    public async Task Handle_WithValidEntryImage_ReturnsAutoCheckIn()
    {
        var handler = CreateHandler(
            lotApproved: true,
            canOperate: true,
            ocrResult: new PlateOcrResult("51F-782.91", "51F78291", 0.98, "Car"),
            hasOpenSession: false);

        var command = new RecognizePlateCommand(TestLotId, "data:image/jpeg;base64,validimage123", "Entry", TestStaffId);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("51F78291", result.Value.NormalizedPlate);
        Assert.Equal("Car", result.Value.VehicleType);
        Assert.True(result.Value.IsConfident);
        Assert.Equal("AutoCheckIn", result.Value.SuggestedAction);
    }

    [Fact]
    public async Task Handle_WithExistingOpenSessionOnEntry_ReturnsSessionAlreadyOpen()
    {
        var handler = CreateHandler(
            lotApproved: true,
            canOperate: true,
            ocrResult: new PlateOcrResult("51F-782.91", "51F78291", 0.98, "Car"),
            hasOpenSession: true);

        var command = new RecognizePlateCommand(TestLotId, "data:image/jpeg;base64,validimage123", "Entry", TestStaffId);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("SessionAlreadyOpen", result.Value.SuggestedAction);
    }

    [Fact]
    public async Task Handle_WithExactMatchOnExit_ReturnsAutoCheckOutAndMatchedSession()
    {
        var session = CreateSession("51F78291", ParkingSessionStatus.Active);
        var repo = new FakeParkingSessionRepository { OpenSession = session };

        var handler = CreateHandler(
            lotApproved: true,
            canOperate: true,
            ocrResult: new PlateOcrResult("51F-782.91", "51F78291", 0.98, "Car"),
            repo: repo);

        var command = new RecognizePlateCommand(TestLotId, "data:image/jpeg;base64,validimage123", "Exit", TestStaffId);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("AutoCheckOut", result.Value.SuggestedAction);
        Assert.NotNull(result.Value.MatchedSession);
        Assert.Equal(session.Id, result.Value.MatchedSession.SessionId);
    }

    [Fact]
    public async Task Handle_WithFuzzyMatchOnExit_ReturnsConfirmCheckOutAndMatchedSession()
    {
        // Active session plate in lot is 51F78291
        var session = CreateSession("51F78291", ParkingSessionStatus.Active);
        var repo = new FakeParkingSessionRepository
        {
            // Exact search for 51F7B291 returns null
            OpenSession = null,
            ActiveSessions = [session]
        };

        // OCR engine misread '8' as 'B' -> "51F7B291"
        var handler = CreateHandler(
            lotApproved: true,
            canOperate: true,
            ocrResult: new PlateOcrResult("51F-7B2.91", "51F7B291", 0.88, "Car"),
            repo: repo);

        var command = new RecognizePlateCommand(TestLotId, "data:image/jpeg;base64,validimage123", "Exit", TestStaffId);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("ConfirmCheckOut", result.Value.SuggestedAction);
        Assert.NotNull(result.Value.MatchedSession);
        Assert.Equal(session.Id, result.Value.MatchedSession.SessionId);
    }

    private static RecognizePlateCommandHandler CreateHandler(
        bool lotApproved,
        bool canOperate,
        PlateOcrResult? ocrResult = null,
        bool hasOpenSession = false,
        FakeParkingSessionRepository? repo = null)
    {
        var lotReader = new FakeLotDetailReader(lotApproved);
        var lotAccess = new FakeLotAccess(canOperate);
        var ocr = new FakeOcrEngine(ocrResult ?? new PlateOcrResult("51F-782.91", "51F78291", 0.95, "Car"));
        var sessions = repo ?? new FakeParkingSessionRepository { HasOpen = hasOpenSession };
        var clock = new FakeClock(DateTimeOffset.UtcNow);

        return new RecognizePlateCommandHandler(lotReader, lotAccess, ocr, sessions, clock);
    }

    private static ParkingSession CreateSession(string plate, ParkingSessionStatus status)
    {
        return ParkingSession.Open(
            Guid.NewGuid(),
            TestLotId,
            plate,
            VehicleType.Car,
            DateTimeOffset.UtcNow.AddMinutes(-30),
            "test-qr-token-12345678901234567890123456789012",
            userId: null,
            vehicleId: null);
    }

    private sealed class FakeLotDetailReader(bool approved) : IParkingLotDetailReader
    {
        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult(approved
                ? new ParkingLotDetail(parkingLotId, "Test Lot", "Address", 10.7m, 106.6m, "Standard")
                : null);
    }

    private sealed class FakeLotAccess(bool canOperate) : IParkingLotAccess
    {
        public Task<bool> CanOperateAsync(Guid accountId, Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult(canOperate);
    }

    private sealed class FakeOcrEngine(PlateOcrResult result) : ILicensePlateOcrEngine
    {
        public Task<PlateOcrResult> RecognizePlateAsync(string imagePayload, string? lotId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class FakeParkingSessionRepository : IParkingSessionRepository
    {
        public bool HasOpen { get; set; }
        public ParkingSession? OpenSession { get; set; }
        public List<ParkingSession> ActiveSessions { get; set; } = [];

        public Task<bool> HasOpenSessionAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) =>
            Task.FromResult(HasOpen);

        public Task<ParkingSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<ParkingSession?>(null);

        public Task<ParkingSession?> FindByIdForUpdateAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<ParkingSession?>(null);

        public Task<IReadOnlyList<ParkingSession>> ListOpenByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ParkingSession>>([]);

        public Task<ParkingSession?> FindOpenByQrTokenAsync(string qrToken, CancellationToken cancellationToken) =>
            Task.FromResult<ParkingSession?>(null);

        public Task<(IReadOnlyList<ParkingSession> Items, int TotalCount)> ListByLotAsync(
            Guid lotId, ParkingSessionStatus? status, int skip, int take, CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<ParkingSession>)ActiveSessions, ActiveSessions.Count));

        public Task<ParkingSession?> FindOpenByPlateAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) =>
            Task.FromResult(OpenSession);

        public void Add(ParkingSession session) { }

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
