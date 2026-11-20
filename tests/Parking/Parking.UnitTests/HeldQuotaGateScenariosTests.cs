using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Booking;
using ParkingSystem.Parking.Application.Common.Identity;
using ParkingSystem.Parking.Application.Common.Persistence;
using ParkingSystem.Parking.Application.Incidents;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;
using ParkingSystem.Parking.Application.ParkingLayout.ZoneCapacity;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Application.ParkingSessions.OpenParkingSession;
using ParkingSystem.Parking.Domain.Incidents;
using ParkingSystem.Parking.Domain.ParkingSessions;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

/// <summary>
/// Verifies the 5 core held quota scenarios at the entry gate according to WP-REAL-HELD-QUOTA and Decision 09/10:
/// 1. Scenario 1 (Happy Path - Quota in effect): Booking returns heldQuota = 2. Available = 2. Walk-in rejected (available - 2 <= 0).
/// 2. Scenario 2 (Happy Path - Walk-in permitted): Booking returns heldQuota = 2. Available = 5. Walk-in permitted (3 walk-in places remain).
/// 3. Scenario 3 (Resilience - Booking Error with Cache): Has recent cache (heldQuota = 1, <= 60s). Booking fails (500/timeout) -> Reuses cache (1).
/// 4. Scenario 4 (Resilience - Booking Error without Cache - Decision 09/10): No cache and Booking fails -> Falls back to 0, does NOT reject walk-in.
/// 5. Scenario 5 (Cache Expiry): Cache > 60s -> Calls Booking again to refresh held quota.
/// </summary>
public sealed class HeldQuotaGateScenariosTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid StaffId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly OpenParkingSessionCommand WalkInCarCommand = new(LotId, "51A-999.99", "Car", StaffId);

    private readonly MovableClock clock = new(new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero));
    private readonly HeldQuotaCache cache = new();
    private readonly HeldQuotaOptions options = new()
    {
        Deadline = TimeSpan.FromMilliseconds(200),
        MaxAge = TimeSpan.FromSeconds(60)
    };

    [Fact]
    public async Task Scenario1_QuotaInEffect_WalkInCarIsRefused_WhenAvailableEqualsHeldQuota()
    {
        // Arrange: Booking holds 2 slots. Physical lot has 2 available slots.
        var reader = new FakeBookingReader { Held = 2 };
        var provider = CreateProvider(reader);
        var slots = new FakeSlotDesignator(available: 2);
        var repository = new FakeParkingSessionRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateGateHandler(repository, slots, provider, unitOfWork);

        // Act: Walk-in car arrives at gate
        var result = await handler.Handle(WalkInCarCommand, TestContext.Current.CancellationToken);

        // Assert: Refused with WalkInFull because available (2) - held (2) <= 0
        Assert.False(result.IsSuccess);
        Assert.Equal(OpenParkingSessionErrors.WalkInFull, result.Error);
        Assert.Empty(repository.Added);
        Assert.False(unitOfWork.Committed);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task Scenario2_WalkInPermitted_WhenAvailableExceedsHeldQuota()
    {
        // Arrange: Booking holds 2 slots. Physical lot has 5 available slots.
        var reader = new FakeBookingReader { Held = 2 };
        var provider = CreateProvider(reader);
        var slots = new FakeSlotDesignator(available: 5);
        var repository = new FakeParkingSessionRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateGateHandler(repository, slots, provider, unitOfWork);

        // Act: Walk-in car arrives at gate
        var result = await handler.Handle(WalkInCarCommand, TestContext.Current.CancellationToken);

        // Assert: Admitted because available (5) - held (2) = 3 remaining walk-in spots
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(repository.Added);
        Assert.True(unitOfWork.Committed);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task Scenario3_Resilience_BookingErrorWithRecentCache_ReusesCachedQuotaWithoutDisruptingGate()
    {
        // Arrange: Initial query succeeds at T0, caching heldQuota = 1
        var reader = new FakeBookingReader { Held = 1 };
        var provider = CreateProvider(reader);
        var slots = new FakeSlotDesignator(available: 5);
        var repository = new FakeParkingSessionRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateGateHandler(repository, slots, provider, unitOfWork);

        // Warm up cache at T0
        var initialHeld = await provider.GetHeldReservationsAsync(LotId, TestContext.Current.CancellationToken);
        Assert.Equal(1, initialHeld);
        Assert.Equal(1, reader.Calls);

        // Advance 30s (< 60s max age) and simulate Booking failure (500 or timeout)
        clock.Advance(TimeSpan.FromSeconds(30));
        reader.Fails = true;

        // Act: Next walk-in arrives, Booking is down
        var result = await handler.Handle(WalkInCarCommand, TestContext.Current.CancellationToken);

        // Assert: Successfully admitted using cached quota = 1 (5 - 1 = 4 > 0). Gate is not disrupted!
        Assert.True(result.IsSuccess);
        Assert.True(unitOfWork.Committed);
        Assert.Equal(2, reader.Calls); // Called Booking, failed, fell back to cache

        // Sub-check: If available slots was 1, cached quota (1) blocks walk-in (1 - 1 <= 0)
        var fullSlots = new FakeSlotDesignator(available: 1);
        var fullHandler = CreateGateHandler(new FakeParkingSessionRepository(), fullSlots, provider, new FakeUnitOfWork());
        var blockedResult = await fullHandler.Handle(WalkInCarCommand, TestContext.Current.CancellationToken);
        Assert.False(blockedResult.IsSuccess);
        Assert.Equal(OpenParkingSessionErrors.WalkInFull, blockedResult.Error);
    }

    [Fact]
    public async Task Scenario4_Resilience_BookingErrorWithoutCache_FallsBackToZeroAndDoesNotRejectWalkIn_Decision0910()
    {
        // Arrange: No cached quota exists. Booking fails immediately with 500/timeout.
        var reader = new FakeBookingReader { Fails = true };
        var provider = CreateProvider(reader);
        var slots = new FakeSlotDesignator(available: 2);
        var repository = new FakeParkingSessionRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateGateHandler(repository, slots, provider, unitOfWork);

        // Act: Walk-in car arrives when Booking is completely unreachable
        var result = await handler.Handle(WalkInCarCommand, TestContext.Current.CancellationToken);

        // Assert: Decision 09/10: Falls back to heldQuota = 0 with warning log.
        // Gate does NOT reject walk-in car due to unknown quota; car is admitted because available (2) - 0 = 2 > 0.
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(repository.Added);
        Assert.True(unitOfWork.Committed);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task Scenario5_CacheExpiry_CallsBookingAgainAfter60SecondsToRefreshQuota()
    {
        // Arrange: Initial query at T0 caches heldQuota = 1
        var reader = new FakeBookingReader { Held = 1 };
        var provider = CreateProvider(reader);
        var slots = new FakeSlotDesignator(available: 3);
        var repository = new FakeParkingSessionRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateGateHandler(repository, slots, provider, unitOfWork);

        // Initial call at T0
        var firstResult = await handler.Handle(WalkInCarCommand, TestContext.Current.CancellationToken);
        Assert.True(firstResult.IsSuccess);
        Assert.Equal(1, reader.Calls);

        // Advance 65 seconds (> 60s cache expiry). Booking now reports heldQuota = 3 (new reservations made).
        clock.Advance(TimeSpan.FromSeconds(65));
        reader.Held = 3;

        // Act: Next walk-in car arrives at T0 + 65s
        var secondResult = await handler.Handle(
            new OpenParkingSessionCommand(LotId, "51B-888.88", "Car", StaffId),
            TestContext.Current.CancellationToken);

        // Assert: Booking was called again (calls = 2), new quota (3) is in effect!
        // Since available was 3 and held is now 3, 3 - 3 <= 0 -> WalkInFull!
        Assert.Equal(2, reader.Calls);
        Assert.False(secondResult.IsSuccess);
        Assert.Equal(OpenParkingSessionErrors.WalkInFull, secondResult.Error);
    }

    private HeldQuotaProvider CreateProvider(FakeBookingReader reader) =>
        new(reader, cache, options, clock, NullLogger<HeldQuotaProvider>.Instance);

    private OpenParkingSessionCommandHandler CreateGateHandler(
        FakeParkingSessionRepository repository,
        FakeSlotDesignator slots,
        IHeldQuotaProvider quotaProvider,
        FakeUnitOfWork unitOfWork)
    {
        return new OpenParkingSessionCommandHandler(
            repository,
            new FakeParkingLotReader(approved: true),
            new FakeLotAccess(allowed: true),
            new FakePlateOwnerLookup(null),
            clock,
            new FakeMotorbikeZones(),
            unitOfWork,
            slots,
            quotaProvider,
            new FakeZoneLabels(),
            new FakeBookingCheckInClient(),
            new FakeNoSlotReporter(),
            new FakeAvailableSlots(slots.Available),
            new FakeIncidents());
    }

    private sealed class FakeBookingReader : IHeldQuotaReader
    {
        public int Held { get; set; }
        public bool Fails { get; set; }
        public int Calls { get; private set; }

        public Task<int> CountHeldReservationsAsync(Guid lotId, CancellationToken cancellationToken)
        {
            Calls++;
            if (Fails)
                return Task.FromException<int>(new HttpRequestException("Booking service unavailable", null, System.Net.HttpStatusCode.InternalServerError));
            return Task.FromResult(Held);
        }
    }

    private sealed class MovableClock(DateTimeOffset initial) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = initial;
        public void Advance(TimeSpan by) => UtcNow += by;
    }

    private sealed class FakeParkingSessionRepository : IParkingSessionRepository
    {
        public List<ParkingSession> Added { get; } = [];
        public List<ParkingSession> Saved { get; } = [];

        public Task<bool> HasOpenSessionAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<ParkingSession>> ListOpenByUserAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ParkingSession?> FindOpenByQrTokenAsync(string qrToken, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<ParkingSession> Items, int TotalCount)> ListByLotAsync(
            Guid lotId, ParkingSessionStatus? status, int skip, int take, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ParkingSession?> FindOpenByPlateAsync(Guid lotId, string normalizedPlate, CancellationToken cancellationToken) =>
            Task.FromResult<ParkingSession?>(null);

        public Task<ParkingSession?> FindByIdAsync(Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(Added.FirstOrDefault(session => session.Id == sessionId));

        public Task<ParkingSession?> FindByIdForUpdateAsync(Guid sessionId, CancellationToken cancellationToken) =>
            FindByIdAsync(sessionId, cancellationToken);

        public void Add(ParkingSession session) => Added.Add(session);

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved.AddRange(Added.Except(Saved));
            return Task.FromResult(true);
        }
    }

    private sealed class FakeUnitOfWork : IParkingUnitOfWork, IParkingTransaction
    {
        public bool Committed { get; private set; }

        public Task<IParkingTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IParkingTransaction>(this);

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeSlotDesignator(int available = 5) : ISlotDesignator
    {
        public static readonly Guid CarZoneId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        public static readonly Guid SlotId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        public int Available { get; } = available;

        public Task<int> LockLotAndCountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult(Available);

        public Task<DesignatedSlot?> DesignateCarSlotAsync(Guid lotId, Guid sessionId, DateTimeOffset assignedAt, CancellationToken cancellationToken) =>
            Task.FromResult<DesignatedSlot?>(new DesignatedSlot(SlotId, CarZoneId, "A-01"));

        public Task<IReadOnlyDictionary<Guid, string>> FindSlotCodesAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string?> ReleaseSessionSlotAsync(Guid sessionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeAvailableSlots(int count) : IAvailableCarSlotCounter
    {
        public Task<int> CountAvailableCarSlotsAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult(count);
    }

    private sealed class FakeParkingLotReader(bool approved) : IParkingLotDetailReader
    {
        public Task<ParkingLotDetail?> GetApprovedByIdAsync(Guid parkingLotId, CancellationToken cancellationToken) =>
            Task.FromResult(approved ? new ParkingLotDetail(parkingLotId, "Lot A", "1 Test Street", 10.77m, 106.70m, "Flat") : null);
    }

    private sealed class FakeLotAccess(bool allowed) : IParkingLotAccess
    {
        public Task<bool> CanOperateAsync(Guid accountId, Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult(allowed);
    }

    private sealed class FakePlateOwnerLookup(PlateOwner? owner) : IPlateOwnerLookup
    {
        public Task<PlateOwner?> FindByPlateAsync(string normalizedPlate, CancellationToken cancellationToken) =>
            Task.FromResult(owner);
    }

    private sealed class FakeMotorbikeZones : IMotorbikeZoneCounter
    {
        public static readonly Guid ZoneId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        public Task<MotorbikeSpaceTaken?> TakeSpaceAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult<MotorbikeSpaceTaken?>(new MotorbikeSpaceTaken(ZoneId, "M2", "Tầng 2", 79));

        public Task ReleaseSpaceAsync(Guid zoneId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<MotorbikeAvailability?> GetAvailabilityAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult<MotorbikeAvailability?>(new MotorbikeAvailability([new MotorbikeZoneAvailability(ZoneId, "M2", "Tầng 2", 80, 1)]));
    }

    private sealed class FakeZoneLabels : IZoneLabelReader
    {
        public Task<IReadOnlyDictionary<Guid, ZoneLabel>> GetLabelsAsync(IReadOnlyCollection<Guid> zoneIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, ZoneLabel>>(
                zoneIds.Where(id => id == FakeSlotDesignator.CarZoneId).ToDictionary(id => id, _ => new ZoneLabel("CAR-A", "Tầng 1")));
    }

    private sealed class FakeBookingCheckInClient : IBookingCheckInClient
    {
        public Task<CheckedInReservation?> CheckInReservationAsync(
            Guid lotId, string normalizedPlate, DateTimeOffset entryTime, Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult<CheckedInReservation?>(null);
    }

    private sealed class FakeNoSlotReporter : IBookingNoSlotReporter
    {
        public Task<Guid?> ReportNoSuitableSlotAsync(
            Guid lotId, string normalizedPlate, DateTimeOffset arrivedAt, CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(null);
    }

    private sealed class FakeIncidents : IIncidentRepository
    {
        public void Add(Incident incident) { }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Incident?> FindByIdAsync(Guid incidentId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Incident?> FindByIdForUpdateAsync(Guid incidentId, Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult<Incident?>(null);

        public Task<IReadOnlyList<Incident>> ListOpenByLotAsync(Guid lotId, IncidentType incidentType, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Incident>>([]);

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }
}
