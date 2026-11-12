using MediatR;
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

    private static async Task<Result<OpenedParkingSession>> OpenCarSessionAsync(
        ServiceProvider serviceProvider, Guid lotId, string plate, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new OpenParkingSessionCommand(lotId, plate, nameof(VehicleType.Car), StaffId), cancellationToken);
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

    private async Task<ServiceProvider> CreateMigratedServiceProviderAsync(int held, CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        services.AddApplicationServices();
        services.AddSingleton<IBookingCheckInClient, NoMatchingBookingCheckInClient>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPlateOwnerLookup, NoPlateOwner>();
        services.AddSingleton<IBookingCheckInClient, NoBookingCheckInClient>();
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

    private sealed class FixedHeldQuota(int held) : IHeldQuotaReader
    {
        public Task<int> CountHeldReservationsAsync(Guid lotId, CancellationToken cancellationToken) => Task.FromResult(held);
    }

    private sealed class NoPlateOwner : IPlateOwnerLookup
    {
        public Task<PlateOwner?> FindByPlateAsync(string normalizedPlate, CancellationToken cancellationToken) => Task.FromResult<PlateOwner?>(null);
    }
}
