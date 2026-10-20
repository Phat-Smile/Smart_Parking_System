using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application;
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
    public async Task Concurrent_motorbike_entries_never_exceed_the_zone_capacity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var (lotId, zoneId) = await SeedLotWithMotorbikeZoneAsync(serviceProvider, capacity: 5, cancellationToken);

        // 20 motorbikes reach two gates at the same time; only 5 spaces exist.
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(index =>
            OpenMotorbikeSessionAsync(serviceProvider, lotId, $"59X1-{index:000}.00", cancellationToken)));

        Assert.Equal(5, results.Count(result => result.IsSuccess));
        Assert.Equal(15, results.Count(result => result.Error == OpenParkingSessionErrors.MotorbikeZoneFull));
        Assert.All(results.Where(result => result.IsSuccess), result => Assert.Equal(zoneId, result.Value!.ZoneId));

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        Assert.Equal(5, (await dbContext.Zones.AsNoTracking().SingleAsync(zone => zone.Id == zoneId, cancellationToken)).CurrentVehicleCount);
        Assert.Equal(5, await dbContext.ParkingSessions.CountAsync(session => session.ZoneId == zoneId, cancellationToken));
    }

    [Fact]
    public async Task A_space_taken_in_a_transaction_that_is_not_committed_is_given_back()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var (lotId, _) = await SeedLotWithMotorbikeZoneAsync(serviceProvider, capacity: 5, cancellationToken);

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            // Same as the open handler when the session save fails: the transaction is disposed without commit.
            await using var transaction = await scope.ServiceProvider.GetRequiredService<IParkingUnitOfWork>().BeginTransactionAsync(cancellationToken);
            Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IMotorbikeZoneCounter>().TakeSpaceAsync(lotId, cancellationToken));
        }

        Assert.Equal(new MotorbikeAvailability(5, 0), await GetAvailabilityAsync(serviceProvider, lotId, cancellationToken));
    }

    [Fact]
    public async Task Releasing_a_space_lowers_the_count_but_never_below_zero()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var (lotId, zoneId) = await SeedLotWithMotorbikeZoneAsync(serviceProvider, capacity: 3, cancellationToken);
        Assert.True((await OpenMotorbikeSessionAsync(serviceProvider, lotId, "59X1-111.11", cancellationToken)).IsSuccess);
        Assert.Equal(new MotorbikeAvailability(3, 1), await GetAvailabilityAsync(serviceProvider, lotId, cancellationToken));

        // The exit flow (US-PAY-006) gives the space back; a second release must not push the count below 0.
        for (var release = 0; release < 2; release++)
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IMotorbikeZoneCounter>().ReleaseSpaceAsync(zoneId, cancellationToken);
        }

        Assert.Equal(new MotorbikeAvailability(3, 0), await GetAvailabilityAsync(serviceProvider, lotId, cancellationToken));
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

    // Owners cannot declare a layout yet (US-OWNER-003), so the test builds one: an approved lot, the floor the owner
    // registered for motorbikes, and its single motorbike zone with the lot-wide capacity.
    private static async Task<(Guid LotId, Guid ZoneId)> SeedLotWithMotorbikeZoneAsync(
        ServiceProvider serviceProvider, int capacity, CancellationToken cancellationToken)
    {
        var lotId = await SeedApprovedLotAsync(serviceProvider, cancellationToken);
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var floor = Floor.Create(Guid.NewGuid(), lotId, "Ground");
        var zone = Zone.Create(Guid.NewGuid(), floor.Id, "M", VehicleType.Motorbike, capacity, hasSlotSensor: false);
        dbContext.Floors.Add(floor);
        dbContext.Zones.Add(zone);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (lotId, zone.Id);
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
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPlateOwnerLookup, NoPlateOwner>();
        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(cancellationToken);
        return serviceProvider;
    }

    private sealed class NoPlateOwner : IPlateOwnerLookup
    {
        public Task<PlateOwner?> FindByPlateAsync(string normalizedPlate, CancellationToken cancellationToken) => Task.FromResult<PlateOwner?>(null);
    }
}
