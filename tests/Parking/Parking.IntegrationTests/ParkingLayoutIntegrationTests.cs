using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Application.ParkingSessions.CancelParkingSession;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class ParkingLayoutIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset EntryTime = new(2026, 10, 11, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Concurrent_designations_never_share_a_slot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedLotAsync(serviceProvider, carSlotCodes: ["A-01", "A-02", "A-03", "A-04", "A-05"], cancellationToken);

        // BR-04.g: 20 cars enter at the same time, only 5 slots exist.
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => DesignateAsync(serviceProvider, lotId, Guid.NewGuid(), cancellationToken)));

        var designated = results.Where(slot => slot is not null).Select(slot => slot!).ToList();
        Assert.Equal(5, designated.Count);
        Assert.Equal(5, designated.Select(slot => slot.SlotId).Distinct().Count());
        Assert.Equal(15, results.Count(slot => slot is null));
    }

    [Fact]
    public async Task Only_available_car_slots_are_designated_and_counted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedLotAsync(
            serviceProvider,
            carSlotCodes: ["A-01", "A-02", "A-03"],
            cancellationToken,
            maintenanceSlotCodes: ["A-01"],
            unavailableSlotCodes: ["A-02"],
            motorbikeSlotCodes: ["M-01"]);
        var otherLotId = await SeedLotAsync(serviceProvider, carSlotCodes: ["B-01", "B-02"], cancellationToken);

        Assert.Equal(1, await CountAvailableAsync(serviceProvider, lotId, cancellationToken));
        Assert.Equal(2, await CountAvailableAsync(serviceProvider, otherLotId, cancellationToken));

        var first = await DesignateAsync(serviceProvider, lotId, Guid.NewGuid(), cancellationToken);
        Assert.Equal("A-03", first!.SlotCode);

        // Maintenance, Unavailable and motorbike slots are never given out; the lot is now full for cars.
        Assert.Null(await DesignateAsync(serviceProvider, lotId, Guid.NewGuid(), cancellationToken));
        Assert.Equal(0, await CountAvailableAsync(serviceProvider, lotId, cancellationToken));
        Assert.Equal(2, await CountAvailableAsync(serviceProvider, otherLotId, cancellationToken));
    }

    [Fact]
    public async Task Designated_slot_stores_the_dispatch_layer_and_a_matching_effective_status()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedLotAsync(serviceProvider, carSlotCodes: ["A-01"], cancellationToken);
        var sessionId = Guid.NewGuid();

        var designated = await DesignateAsync(serviceProvider, lotId, sessionId, cancellationToken);

        await using var scope = serviceProvider.CreateAsyncScope();
        var slot = await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().ParkingSlots
            .SingleAsync(item => item.Id == designated!.SlotId, cancellationToken);
        Assert.Equal(DispatchState.Assigned, slot.DispatchState);
        Assert.Equal(sessionId, slot.AssignedSessionId);
        Assert.Equal(EntryTime, slot.AssignedAt);
        Assert.Equal(1, slot.RowVersion);
        // The SQL update and the C# priority rule must agree.
        Assert.Equal(ParkingSlot.ComputeEffectiveStatus(slot.SensorState, slot.OverrideState, slot.DispatchState), slot.EffectiveStatus);
        Assert.Equal(SlotEffectiveStatus.Assigned, slot.EffectiveStatus);
    }

    [Fact]
    public async Task A_session_cannot_hold_two_slots()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedLotAsync(serviceProvider, carSlotCodes: ["A-01", "A-02"], cancellationToken);
        var sessionId = Guid.NewGuid();

        Assert.NotNull(await DesignateAsync(serviceProvider, lotId, sessionId, cancellationToken));

        // ERD: assigned_session_id is unique, so the database refuses a second slot for the same session.
        await Assert.ThrowsAnyAsync<Exception>(() => DesignateAsync(serviceProvider, lotId, sessionId, cancellationToken));
        Assert.Equal(1, await CountAvailableAsync(serviceProvider, lotId, cancellationToken));
    }

    [Fact]
    public async Task Cancelling_a_session_returns_its_slot_to_available_in_the_same_save()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedLotAsync(serviceProvider, carSlotCodes: ["A-01"], cancellationToken);
        var session = ParkingSession.Open(Guid.NewGuid(), lotId, "51A-123.45", VehicleType.Car, EntryTime, Guid.NewGuid().ToString("N"), null, null);
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<IParkingSessionRepository>();
            sessions.Add(session);
            Assert.True(await sessions.TrySaveChangesAsync(cancellationToken));
        }

        var designated = await DesignateAsync(serviceProvider, lotId, session.Id, cancellationToken);
        Assert.Equal(0, await CountAvailableAsync(serviceProvider, lotId, cancellationToken));

        // TECH-12: only staff assigned to the lot can cancel its sessions.
        var staffId = Guid.NewGuid();
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var assignments = scope.ServiceProvider.GetRequiredService<IParkingLotAssignmentRepository>();
            assignments.Add(ParkingLotAssignment.Create(lotId, staffId, ParkingLotMemberRole.Staff, Guid.NewGuid(), DateTimeOffset.UtcNow));
            await assignments.SaveChangesAsync(cancellationToken);
        }

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var handler = new CancelParkingSessionCommandHandler(
                scope.ServiceProvider.GetRequiredService<IParkingSessionRepository>(),
                scope.ServiceProvider.GetRequiredService<IParkingLotAccess>(),
                scope.ServiceProvider.GetRequiredService<IAuditLog>(),
                scope.ServiceProvider.GetRequiredService<IClock>(),
                scope.ServiceProvider.GetRequiredService<ISlotDesignator>());

            var result = await handler.Handle(
                new CancelParkingSessionCommand(session.Id, "Vehicle did not enter", staffId), cancellationToken);

            Assert.True(result.IsSuccess);
        }

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            var slot = await dbContext.ParkingSlots.AsNoTracking().SingleAsync(item => item.Id == designated!.SlotId, cancellationToken);
            Assert.Null(slot.DispatchState);
            Assert.Null(slot.AssignedSessionId);
            Assert.Equal(SlotEffectiveStatus.Available, slot.EffectiveStatus);
            Assert.Equal(2, slot.RowVersion);

            var entry = await dbContext.AuditLogEntries.AsNoTracking().SingleAsync(item => item.TargetId == session.Id.ToString(), cancellationToken);
            Assert.Contains("\"releasedSlotCode\": \"A-01\"", entry.Details);
        }

        // The released slot can be designated again.
        Assert.Equal(1, await CountAvailableAsync(serviceProvider, lotId, cancellationToken));
        Assert.Equal("A-01", (await DesignateAsync(serviceProvider, lotId, Guid.NewGuid(), cancellationToken))!.SlotCode);
    }

    private static async Task<DesignatedSlot?> DesignateAsync(
        ServiceProvider serviceProvider, Guid lotId, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISlotDesignator>()
            .DesignateCarSlotAsync(lotId, sessionId, EntryTime, cancellationToken);
    }

    private static async Task<int> CountAvailableAsync(ServiceProvider serviceProvider, Guid lotId, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAvailableSlotReader>()
            .CountAvailableCarSlotsAsync(lotId, cancellationToken);
    }

    // Lot owners cannot declare a layout yet (later story), so the test builds one: an approved lot, one floor,
    // a car zone with the given slots and, when asked, a motorbike zone.
    private static async Task<Guid> SeedLotAsync(
        ServiceProvider serviceProvider,
        string[] carSlotCodes,
        CancellationToken cancellationToken,
        string[]? maintenanceSlotCodes = null,
        string[]? unavailableSlotCodes = null,
        string[]? motorbikeSlotCodes = null)
    {
        var lotId = Guid.NewGuid();
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
            VALUES ({lotId}, 'Layout Test Parking', '1 Demo Street', 10.776530, 106.700981, 'MultiStorey', 'Approved')
            """,
            cancellationToken);

        var floor = Floor.Create(Guid.NewGuid(), lotId, "Ground");
        var carZone = Zone.Create(Guid.NewGuid(), floor.Id, "A", VehicleType.Car, carSlotCodes.Length, hasSlotSensor: true);
        dbContext.Floors.Add(floor);
        dbContext.Zones.Add(carZone);
        foreach (var code in carSlotCodes)
        {
            OverrideState? declaredOverride = maintenanceSlotCodes?.Contains(code) == true ? OverrideState.Maintenance
                : unavailableSlotCodes?.Contains(code) == true ? OverrideState.Unavailable
                : null;
            dbContext.ParkingSlots.Add(ParkingSlot.Create(Guid.NewGuid(), lotId, carZone.Id, code, declaredOverride));
        }

        if (motorbikeSlotCodes is { Length: > 0 })
        {
            var motorbikeZone = Zone.Create(Guid.NewGuid(), floor.Id, "M", VehicleType.Motorbike, 50, hasSlotSensor: false);
            dbContext.Zones.Add(motorbikeZone);
            foreach (var code in motorbikeSlotCodes)
                dbContext.ParkingSlots.Add(ParkingSlot.Create(Guid.NewGuid(), lotId, motorbikeZone.Id, code));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return lotId;
    }

    private async Task<ServiceProvider> CreateMigratedServiceProviderAsync(CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(cancellationToken);
        return serviceProvider;
    }
}
