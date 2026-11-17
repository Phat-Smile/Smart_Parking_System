using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.Common.Booking;
using ParkingSystem.Parking.Application.Common.Identity;
using ParkingSystem.Parking.Application.Common.Persistence;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions.OpenParkingSession;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// US-PARK-008 against real PostgreSQL: the motorbike count must hold under concurrent entries (BR-07.b).
public sealed class MotorbikeZoneIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly Guid StaffId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task Concurrent_motorbike_entries_never_exceed_any_zone_capacity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        // Two motorbike zones on two floors, each with its own capacity (context 6.3): 5 spaces in total.
        var (lotId, zones) = await SeedLotWithMotorbikeZonesAsync(serviceProvider, cancellationToken, ("Tầng 1", "M1", 3), ("Tầng hầm", "M2", 2));

        // 20 motorbikes reach the gates at the same time.
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(index =>
            OpenMotorbikeSessionAsync(serviceProvider, lotId, $"59X1-{index:000}.00", cancellationToken)));

        Assert.Equal(5, results.Count(result => result.IsSuccess));
        Assert.Equal(15, results.Count(result => result.Error == OpenParkingSessionErrors.MotorbikeZoneFull));
        Assert.All(results.Where(result => result.IsSuccess), result => Assert.NotNull(result.Value!.ZoneCode));

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        foreach (var (zoneId, capacity) in new[] { (zones[0], 3), (zones[1], 2) })
        {
            Assert.Equal(capacity, (await dbContext.Zones.AsNoTracking().SingleAsync(zone => zone.Id == zoneId, cancellationToken)).CurrentVehicleCount);
            Assert.Equal(capacity, await dbContext.ParkingSessions.CountAsync(session => session.ZoneId == zoneId, cancellationToken));
        }
    }

    [Fact]
    public async Task A_motorbike_is_sent_to_the_zone_with_the_most_free_spaces_and_told_its_floor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var (lotId, zones) = await SeedLotWithMotorbikeZonesAsync(serviceProvider, cancellationToken, ("Tầng 1", "M1", 2), ("Tầng hầm", "M2", 5));

        var result = await OpenMotorbikeSessionAsync(serviceProvider, lotId, "59X1-111.11", cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(zones[1], result.Value!.ZoneId);
        Assert.Equal("M2", result.Value.ZoneCode);
        Assert.Equal("Tầng hầm", result.Value.FloorName);
        // Spaces left in the whole lot after this entry: 2 + 4.
        Assert.Equal(6, result.Value.AvailableMotorbikeSpaces);

        // The customer view (US-PARK-009 /me) reads the same labels.
        await using var scope = serviceProvider.CreateAsyncScope();
        var labels = await scope.ServiceProvider.GetRequiredService<IZoneLabelReader>().GetLabelsAsync([zones[1]], cancellationToken);
        Assert.Equal(new ZoneLabel("M2", "Tầng hầm"), labels[zones[1]]);
    }

    [Fact]
    public async Task A_space_taken_in_a_transaction_that_is_not_committed_is_given_back()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var (lotId, _) = await SeedLotWithMotorbikeZonesAsync(serviceProvider, cancellationToken, ("Tầng 1", "M1", 5));

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            // Same as the open handler when the session save fails: the transaction is disposed without commit.
            await using var transaction = await scope.ServiceProvider.GetRequiredService<IParkingUnitOfWork>().BeginTransactionAsync(cancellationToken);
            Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IMotorbikeZoneCounter>().TakeSpaceAsync(lotId, cancellationToken));
        }

        Assert.Equal(0, (await GetAvailabilityAsync(serviceProvider, lotId, cancellationToken))!.Occupied);
    }

    [Fact]
    public async Task Releasing_a_space_lowers_the_count_but_never_below_zero()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var (lotId, zones) = await SeedLotWithMotorbikeZonesAsync(serviceProvider, cancellationToken, ("Tầng 1", "M1", 3));
        Assert.True((await OpenMotorbikeSessionAsync(serviceProvider, lotId, "59X1-111.11", cancellationToken)).IsSuccess);
        Assert.Equal(1, (await GetAvailabilityAsync(serviceProvider, lotId, cancellationToken))!.Occupied);

        // The exit flow (US-PAY-006) gives the space back; a second release must not push the count below 0.
        for (var release = 0; release < 2; release++)
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IMotorbikeZoneCounter>().ReleaseSpaceAsync(zones[0], cancellationToken);
        }

        var availability = await GetAvailabilityAsync(serviceProvider, lotId, cancellationToken);
        Assert.Equal(0, availability!.Occupied);
        Assert.Equal(3, availability.Available);
    }

    [Fact]
    public async Task A_lot_without_a_motorbike_zone_refuses_motorbikes_and_has_no_availability()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedApprovedLotAsync(serviceProvider, cancellationToken);

        var result = await OpenMotorbikeSessionAsync(serviceProvider, lotId, "59X1-222.22", cancellationToken);

        Assert.Equal(OpenParkingSessionErrors.MotorbikeZoneNotConfigured, result.Error);
        Assert.Null(await GetAvailabilityAsync(serviceProvider, lotId, cancellationToken));
    }

    private static async Task<BuildingBlocks.Results.Result<OpenedParkingSession>> OpenMotorbikeSessionAsync(
        ServiceProvider serviceProvider, Guid lotId, string plate, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new OpenParkingSessionCommand(lotId, plate, nameof(VehicleType.Motorbike), StaffId), cancellationToken);
    }

    private static async Task<MotorbikeAvailability?> GetAvailabilityAsync(ServiceProvider serviceProvider, Guid lotId, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMotorbikeZoneCounter>().GetAvailabilityAsync(lotId, cancellationToken);
    }

    // Owners cannot declare zones yet (US-OWNER-003), so the test builds them: an approved lot and, for each entry,
    // a floor with one motorbike zone and its own capacity.
    private static async Task<(Guid LotId, Guid[] ZoneIds)> SeedLotWithMotorbikeZonesAsync(
        ServiceProvider serviceProvider, CancellationToken cancellationToken, params (string Floor, string Code, int Capacity)[] zones)
    {
        var lotId = await SeedApprovedLotAsync(serviceProvider, cancellationToken);
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var zoneIds = new List<Guid>();
        foreach (var (floorName, code, capacity) in zones)
        {
            var floor = Floor.Create(Guid.NewGuid(), lotId, floorName);
            var zone = Zone.Create(Guid.NewGuid(), floor.Id, code, VehicleType.Motorbike, capacity, hasSlotSensor: false);
            dbContext.Floors.Add(floor);
            dbContext.Zones.Add(zone);
            zoneIds.Add(zone.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (lotId, zoneIds.ToArray());
    }

    private static async Task<Guid> SeedApprovedLotAsync(ServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var lotId = Guid.NewGuid();
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
            VALUES ({lotId}, 'Motorbike Test Parking', '1 Demo Street', 10.776530, 106.700981, 'Outdoor', 'Approved')
            """,
            cancellationToken);

        // TECH-12: the staff member opening sessions must be assigned to the lot.
        var assignments = scope.ServiceProvider.GetRequiredService<IParkingLotAssignmentRepository>();
        assignments.Add(ParkingLotAssignment.Create(lotId, StaffId, ParkingLotMemberRole.Staff, Guid.NewGuid(), DateTimeOffset.UtcNow));
        await assignments.SaveChangesAsync(cancellationToken);
        return lotId;
    }

    private async Task<ServiceProvider> CreateMigratedServiceProviderAsync(CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        services.AddApplicationServices();
        services.AddSingleton<IBookingCheckInClient, NoMatchingBookingCheckInClient>();
        services.AddSingleton<IBookingNoSlotReporter, NoMatchingBookingCheckInClient>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPlateOwnerLookup, NoPlateOwner>();
        services.AddSingleton<IBookingCheckInClient, NoBookingCheckInClient>();
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

    private sealed class NoPlateOwner : IPlateOwnerLookup
    {
        public Task<PlateOwner?> FindByPlateAsync(string normalizedPlate, CancellationToken cancellationToken) => Task.FromResult<PlateOwner?>(null);
    }
}
