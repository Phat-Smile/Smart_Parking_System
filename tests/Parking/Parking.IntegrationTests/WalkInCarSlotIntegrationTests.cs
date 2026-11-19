using MediatR;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.Common.Booking;
using ParkingSystem.Parking.Application.Common.Identity;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions.CancelParkingSession;
using ParkingSystem.Parking.Application.ParkingSessions.OpenParkingSession;
using ParkingSystem.Parking.Domain.Incidents;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// US-PARK-007 against real PostgreSQL: walk-in capacity = max(0, Available car slots - held reservations) must hold
// when several gates admit cars at the same time (BR-02.d, BR-04.h).
public sealed class WalkInCarSlotIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly Guid StaffId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public async Task Concurrent_walk_in_cars_never_take_the_slots_kept_for_held_reservations()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // 3 Available slots, 2 held for reservations: exactly one walk-in car may enter.
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(held: 2, cancellationToken);
        var lotId = await SeedLotWithCarSlotsAsync(serviceProvider, cancellationToken, "A-01", "A-02", "A-03");

        var results = await Task.WhenAll(Enumerable.Range(1, 5).Select(i =>
            OpenCarSessionAsync(serviceProvider, lotId, $"51A-000.0{i}", cancellationToken)));

        Assert.Single(results, result => result.IsSuccess);
        Assert.All(results.Where(result => !result.IsSuccess), result => Assert.Equal(OpenParkingSessionErrors.WalkInFull, result.Error));

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var assigned = await dbContext.ParkingSlots.AsNoTracking()
            .Where(slot => slot.LotId == lotId && slot.EffectiveStatus == SlotEffectiveStatus.Assigned)
            .ToListAsync(cancellationToken);
        var admitted = results.Single(result => result.IsSuccess).Value!;
        Assert.Equal(admitted.SessionId, Assert.Single(assigned).AssignedSessionId);
        Assert.Equal(1, await dbContext.ParkingSessions.CountAsync(session => session.LotId == lotId, cancellationToken));
    }

    [Fact]
    public async Task Walk_in_cars_get_slots_in_code_order_until_the_lot_is_full()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(held: 0, cancellationToken);
        var lotId = await SeedLotWithCarSlotsAsync(serviceProvider, cancellationToken, "A-02", "A-01");

        var first = await OpenCarSessionAsync(serviceProvider, lotId, "51A-111.11", cancellationToken);
        var second = await OpenCarSessionAsync(serviceProvider, lotId, "51A-222.22", cancellationToken);
        var third = await OpenCarSessionAsync(serviceProvider, lotId, "51A-333.33", cancellationToken);

        Assert.Equal("A-01", first.Value!.SlotCode);
        Assert.Equal("CAR-A", first.Value.ZoneCode);
        Assert.Equal("Tầng 1", first.Value.FloorName);
        Assert.Equal("A-02", second.Value!.SlotCode);
        Assert.Equal(OpenParkingSessionErrors.WalkInFull, third.Error);
    }

    [Fact]
    public async Task Session_reads_return_the_slot_code_and_cancelling_gives_the_slot_back()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(held: 0, cancellationToken);
        var lotId = await SeedLotWithCarSlotsAsync(serviceProvider, cancellationToken, "A-01");
        var opened = (await OpenCarSessionAsync(serviceProvider, lotId, "51A-444.44", cancellationToken)).Value!;

        Assert.Equal("A-01", (await FindSlotCodesAsync(serviceProvider, opened.SessionId, cancellationToken)).GetValueOrDefault(opened.SessionId));

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var cancelled = await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new CancelParkingSessionCommand(opened.SessionId, "Vehicle did not enter", StaffId), cancellationToken);
            Assert.True(cancelled.IsSuccess);
        }

        Assert.Empty(await FindSlotCodesAsync(serviceProvider, opened.SessionId, cancellationToken));
        // The released slot can be given to the next car.
        Assert.Equal("A-01", (await OpenCarSessionAsync(serviceProvider, lotId, "51A-555.55", cancellationToken)).Value!.SlotCode);
    }

    [Fact]
    public async Task Reserved_car_arriving_at_a_full_lot_is_reported_to_booking_and_saved_as_an_incident()
    {
        // US-PARK-004 (BR-02.f, point 35 option a): no check-in, Booking cancels and refunds, staff see the incident.
        var cancellationToken = TestContext.Current.CancellationToken;
        var reservationId = Guid.NewGuid();
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(held: 0, cancellationToken, cancelledReservationId: reservationId);
        var lotId = await SeedLotWithCarSlotsAsync(serviceProvider, cancellationToken, "A-01");
        Assert.True((await OpenCarSessionAsync(serviceProvider, lotId, "51A-111.11", cancellationToken)).IsSuccess);

        var result = await OpenCarSessionAsync(serviceProvider, lotId, "51A-222.22", cancellationToken);
        // Staff retry the same car: Booking returns the same cancelled reservation, still one incident.
        var retried = await OpenCarSessionAsync(serviceProvider, lotId, "51A-222.22", cancellationToken);

        Assert.Equal(OpenParkingSessionErrors.ReservedNoSlot, result.Error);
        Assert.Equal(OpenParkingSessionErrors.ReservedNoSlot, retried.Error);
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var incident = await dbContext.Incidents.AsNoTracking().SingleAsync(item => item.LotId == lotId, cancellationToken);
        Assert.Equal(IncidentType.NoSuitableSlot, incident.IncidentType);
        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.Equal(reservationId, incident.ReservationId);
        Assert.Equal("51A-222.22", incident.ObservedPlate);
        Assert.Equal(1, await dbContext.ParkingSessions.CountAsync(session => session.LotId == lotId, cancellationToken));
    }

    [Fact]
    public async Task Two_gates_using_same_confirmed_reading_only_one_calls_booking_and_replay_after_cancel_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new GateClock();
        var booking = new RecordingBookingCheckIn();
        await using var provider = await CreateMigratedServiceProviderAsync(0, ct, booking: booking, clock: clock);
        var lot = await SeedLotWithCarSlotsAsync(provider, ct, "A-01", "A-02");
        var arrivedAt = clock.UtcNow.AddMinutes(-2);
        var reading = Incident.OpenLowConfidencePlate(Guid.NewGuid(), lot, "51A-123.4S", 0.5m, VehicleType.Car, arrivedAt);
        Assert.True(reading.ConfirmPlate(StaffId, arrivedAt.AddSeconds(1), "51A-123.45", VehicleType.Car));
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            db.Incidents.Add(reading);
            await db.SaveChangesAsync(ct);
        }
        var first = OpenCarSessionAsync(provider, lot, "51A-123.45", ct, reading.Id);
        Task<Result<OpenedParkingSession>> second;
        try
        {
            await booking.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            // PostgreSQL itself proves the incident is already locked before the external Booking call returns.
            await using (var probe = provider.CreateAsyncScope())
            {
                var db = probe.ServiceProvider.GetRequiredService<ParkingDbContext>();
                var blocked = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT id FROM parking.incidents WHERE id = {reading.Id} FOR UPDATE NOWAIT", ct));
                Assert.Equal(PostgresErrorCodes.LockNotAvailable, blocked.SqlState);
            }
            second = OpenCarSessionAsync(provider, lot, "51A-123.45", ct, reading.Id);
        }
        finally
        {
            booking.Release.TrySetResult(true);
        }
        var results = await Task.WhenAll(first, second);
        var winner = Assert.Single(results, result => result.IsSuccess).Value!;
        Assert.Equal(OpenParkingSessionErrors.InvalidGateReading, Assert.Single(results, result => !result.IsSuccess).Error);
        Assert.Equal(1, booking.Calls);
        Assert.Equal(arrivedAt, booking.ArrivedAt);
        Assert.Equal(clock.UtcNow, winner.EntryTime);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            Assert.Equal(winner.SessionId, (await db.Incidents.AsNoTracking().SingleAsync(x => x.Id == reading.Id, ct)).SessionId);
            Assert.Equal(1, await db.ParkingSessions.CountAsync(x => x.LotId == lot, ct));
            var cancelled = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CancelParkingSessionCommand(winner.SessionId, "Vehicle did not enter", StaffId), ct);
            Assert.True(cancelled.IsSuccess);
        }
        var replay = await OpenCarSessionAsync(provider, lot, "51A-123.45", ct, reading.Id);
        Assert.Equal(OpenParkingSessionErrors.InvalidGateReading, replay.Error);
        Assert.Equal(1, booking.Calls);
    }

    private sealed class GateClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    private sealed class RecordingBookingCheckIn : IBookingCheckInClient
    {
        private int calls;
        public int Calls => calls;
        public DateTimeOffset? ArrivedAt { get; private set; }
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<CheckedInReservation?> CheckInReservationAsync(Guid lotId, string normalizedPlate, DateTimeOffset entryTime, Guid sessionId, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            ArrivedAt = entryTime;
            Entered.TrySetResult(true);
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return null;
        }
    }

    private static async Task<Result<OpenedParkingSession>> OpenCarSessionAsync(
        ServiceProvider serviceProvider, Guid lotId, string plate, CancellationToken cancellationToken, Guid? incidentId = null)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new OpenParkingSessionCommand(lotId, plate, nameof(VehicleType.Car), StaffId, incidentId), cancellationToken);
    }

    private static async Task<IReadOnlyDictionary<Guid, string>> FindSlotCodesAsync(
        ServiceProvider serviceProvider, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISlotDesignator>().FindSlotCodesAsync([sessionId], cancellationToken);
    }

    // An approved lot with one car zone on one floor, its slots, and the staff member assigned to the lot (TECH-12).
    private static async Task<Guid> SeedLotWithCarSlotsAsync(ServiceProvider serviceProvider, CancellationToken cancellationToken, params string[] slotCodes)
    {
        var lotId = Guid.NewGuid();
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
            VALUES ({lotId}, 'Walk-in Car Test Parking', '1 Demo Street', 10.776530, 106.700981, 'Outdoor', 'Approved')
            """,
            cancellationToken);

        var floor = Floor.Create(Guid.NewGuid(), lotId, "Tầng 1");
        var zone = Zone.Create(Guid.NewGuid(), floor.Id, "CAR-A", VehicleType.Car, slotCodes.Length, hasSlotSensor: false);
        dbContext.Floors.Add(floor);
        dbContext.Zones.Add(zone);
        foreach (var code in slotCodes)
            dbContext.ParkingSlots.Add(ParkingSlot.Create(Guid.NewGuid(), lotId, zone.Id, code));
        await dbContext.SaveChangesAsync(cancellationToken);

        var assignments = scope.ServiceProvider.GetRequiredService<IParkingLotAssignmentRepository>();
        assignments.Add(ParkingLotAssignment.Create(lotId, StaffId, ParkingLotMemberRole.Staff, Guid.NewGuid(), DateTimeOffset.UtcNow));
        await assignments.SaveChangesAsync(cancellationToken);
        return lotId;
    }

    private async Task<ServiceProvider> CreateMigratedServiceProviderAsync(
        int held, CancellationToken cancellationToken, Guid? cancelledReservationId = null, IBookingCheckInClient? booking = null, IClock? clock = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        services.AddApplicationServices();
        services.AddSingleton<IBookingCheckInClient, NoMatchingBookingCheckInClient>();
        services.AddSingleton<IBookingNoSlotReporter, NoMatchingBookingCheckInClient>();
        services.AddSingleton<IClock>(clock ?? new SystemClock());
        services.AddSingleton<IPlateOwnerLookup, NoPlateOwner>();
        services.AddSingleton<IBookingCheckInClient>(booking ?? new NoBookingCheckInClient());
        services.AddSingleton<IBookingNoSlotReporter>(new FixedNoSlotReporter(cancelledReservationId));
        // Booking contract #4 is still a stub returning 0; the test sets how many reservations are held.
        services.AddSingleton<IHeldQuotaReader>(new FixedHeldQuota(held));
        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(cancellationToken);
        return serviceProvider;
    }

    private sealed class NoBookingCheckInClient : IBookingCheckInClient
    {
        public Task<CheckedInReservation?> CheckInReservationAsync(
            Guid lotId,
            string normalizedPlate,
            DateTimeOffset entryTime,
            Guid sessionId,
            CancellationToken cancellationToken) => Task.FromResult<CheckedInReservation?>(null);
    }

    private sealed class FixedNoSlotReporter(Guid? cancelledReservationId) : IBookingNoSlotReporter
    {
        public Task<Guid?> ReportNoSuitableSlotAsync(Guid lotId, string normalizedPlate, DateTimeOffset arrivedAt, CancellationToken cancellationToken) =>
            Task.FromResult(cancelledReservationId);
    }

    private sealed class FixedHeldQuota(int held) : IHeldQuotaReader
    {
        public Task<int> CountHeldReservationsAsync(Guid lotId, CancellationToken cancellationToken) => Task.FromResult(held);
    }

    private sealed class NoPlateOwner : IPlateOwnerLookup
    {
        public Task<PlateOwner?> FindByPlateAsync(string normalizedPlate, CancellationToken cancellationToken) => Task.FromResult<PlateOwner?>(null);
    }
}
