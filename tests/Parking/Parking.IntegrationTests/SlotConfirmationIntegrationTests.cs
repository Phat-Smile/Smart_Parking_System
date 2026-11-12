using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.ParkingLayout.SlotConfirmation;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.Incidents;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// US-PARK-005 on real PostgreSQL: the timeout job, the staff confirmations and the columns behind them.
public sealed class SlotConfirmationIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private const string NeedsVerificationEvent = "parking.slot-needs-verification.v1";
    private static readonly Guid StaffId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    [Fact]
    public async Task The_job_moves_only_overdue_slots_opens_the_incident_and_writes_the_outbox_row()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedLotAsync(serviceProvider, cancellationToken);
        var overdue = await SeedAssignedSlotAsync(serviceProvider, lotId, "A-01", DateTimeOffset.UtcNow.AddMinutes(-11), cancellationToken);
        var inTime = await SeedAssignedSlotAsync(serviceProvider, lotId, "A-02", DateTimeOffset.UtcNow.AddMinutes(-9), cancellationToken);

        await RunJobAsync(serviceProvider, cancellationToken);
        await RunJobAsync(serviceProvider, cancellationToken); // a second tick changes nothing

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        Assert.Equal(SlotEffectiveStatus.NeedsVerification, (await FindSlotAsync(dbContext, overdue.SlotId, cancellationToken)).EffectiveStatus);
        Assert.Equal(SlotEffectiveStatus.Assigned, (await FindSlotAsync(dbContext, inTime.SlotId, cancellationToken)).EffectiveStatus);
        var incident = await dbContext.Incidents.AsNoTracking().SingleAsync(item => item.LotId == lotId, cancellationToken);
        Assert.Equal(IncidentType.ParkConfirmTimeout, incident.IncidentType);
        Assert.Equal(overdue.SlotId, incident.SlotId);
        Assert.Equal(overdue.SessionId, incident.SessionId);
        Assert.Equal(1, await CountEventsAsync(dbContext, overdue.SlotId, cancellationToken));
    }

    [Fact]
    public async Task The_owner_is_told_once_after_30_minutes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedLotAsync(serviceProvider, cancellationToken);
        var seeded = await SeedAssignedSlotAsync(serviceProvider, lotId, "A-01", DateTimeOffset.UtcNow.AddHours(-1), cancellationToken);
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            var slot = await dbContext.ParkingSlots.SingleAsync(item => item.Id == seeded.SlotId, cancellationToken);
            slot.MarkNeedsVerification(DateTimeOffset.UtcNow.AddMinutes(-31));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await RunJobAsync(serviceProvider, cancellationToken);
        await RunJobAsync(serviceProvider, cancellationToken);

        await using var check = serviceProvider.CreateAsyncScope();
        var context = check.ServiceProvider.GetRequiredService<ParkingDbContext>();
        Assert.NotNull((await FindSlotAsync(context, seeded.SlotId, cancellationToken)).OwnerNotifiedAt);
        Assert.Equal(1, await CountEventsAsync(context, seeded.SlotId, cancellationToken));
    }

    [Fact]
    public async Task Confirmed_parked_is_stored_and_leaving_clears_it()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedLotAsync(serviceProvider, cancellationToken);
        var seeded = await SeedAssignedSlotAsync(serviceProvider, lotId, "A-01", DateTimeOffset.UtcNow, cancellationToken);

        var result = await SendAsync(serviceProvider, new ConfirmSlotParkedCommand(seeded.SlotId, null, StaffId), cancellationToken);

        Assert.True(result.IsSuccess);
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var slot = await dbContext.ParkingSlots.SingleAsync(item => item.Id == seeded.SlotId, cancellationToken);
        Assert.Equal(SlotEffectiveStatus.Occupied, slot.EffectiveStatus);
        Assert.NotNull(slot.ManuallyParkedAt);
        Assert.Equal(seeded.SessionId, slot.AssignedSessionId);

        slot.ReleaseAssignment();
        await dbContext.SaveChangesAsync(cancellationToken);
        Assert.Equal(SlotEffectiveStatus.Available, (await FindSlotAsync(dbContext, seeded.SlotId, cancellationToken)).EffectiveStatus);
    }

    [Fact]
    public async Task Two_staff_at_once_only_one_wins()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedLotAsync(serviceProvider, cancellationToken);
        var seeded = await SeedAssignedSlotAsync(serviceProvider, lotId, "A-01", DateTimeOffset.UtcNow.AddMinutes(-11), cancellationToken);
        await RunJobAsync(serviceProvider, cancellationToken);

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            SendAsync(serviceProvider, new ConfirmSlotParkedCommand(seeded.SlotId, null, StaffId), cancellationToken)));

        Assert.Equal(1, results.Count(result => result.IsSuccess));
        Assert.All(results.Where(result => result.IsFailure), result =>
            Assert.Contains(result.Error, new[] { ConfirmSlotErrors.SlotChanged, ConfirmSlotErrors.NotConfirmable }));
    }

    [Fact]
    public async Task Confirm_empty_with_an_open_session_keeps_the_session_and_the_incident()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedLotAsync(serviceProvider, cancellationToken);
        var seeded = await SeedAssignedSlotAsync(serviceProvider, lotId, "A-01", DateTimeOffset.UtcNow.AddMinutes(-11), cancellationToken);
        await RunJobAsync(serviceProvider, cancellationToken);

        var result = await SendAsync(serviceProvider, new ConfirmSlotEmptyCommand(seeded.SlotId, "no car here", StaffId), cancellationToken);

        Assert.True(result.IsSuccess);
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var slot = await FindSlotAsync(dbContext, seeded.SlotId, cancellationToken);
        Assert.Equal(SlotEffectiveStatus.Available, slot.EffectiveStatus);
        Assert.Null(slot.AssignedSessionId);
        var session = await dbContext.ParkingSessions.AsNoTracking().SingleAsync(item => item.Id == seeded.SessionId, cancellationToken);
        Assert.Equal(ParkingSessionStatus.Active, session.Status);
        var incident = await dbContext.Incidents.AsNoTracking().SingleAsync(item => item.SlotId == seeded.SlotId, cancellationToken);
        Assert.Equal(IncidentStatus.Open, incident.Status);
    }

    private static Task<ParkingSlot> FindSlotAsync(ParkingDbContext dbContext, Guid slotId, CancellationToken cancellationToken) =>
        dbContext.ParkingSlots.AsNoTracking().SingleAsync(item => item.Id == slotId, cancellationToken);

    // Payload is jsonb, so it is compared here rather than in the query.
    private static async Task<int> CountEventsAsync(ParkingDbContext dbContext, Guid slotId, CancellationToken cancellationToken)
    {
        var payloads = await dbContext.OutboxMessages.AsNoTracking()
            .Where(message => message.EventType == NeedsVerificationEvent)
            .Select(message => message.Payload)
            .ToListAsync(cancellationToken);
        return payloads.Count(payload => payload.Contains(slotId.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private static async Task RunJobAsync(ServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CheckParkConfirmTimeoutsCommand(), cancellationToken);
    }

    private static async Task<Result<ConfirmedSlot>> SendAsync(
        ServiceProvider serviceProvider, IRequest<Result<ConfirmedSlot>> command, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, cancellationToken);
    }

    private static async Task<Guid> SeedLotAsync(ServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var lotId = Guid.NewGuid();
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
            VALUES ({lotId}, 'Slot Confirmation Test Parking', '1 Demo Street', 10.776530, 106.700981, 'Outdoor', 'Approved')
            """,
            cancellationToken);

        var assignments = scope.ServiceProvider.GetRequiredService<IParkingLotAssignmentRepository>();
        assignments.Add(ParkingLotAssignment.Create(lotId, StaffId, ParkingLotMemberRole.Staff, Guid.NewGuid(), DateTimeOffset.UtcNow));
        await assignments.SaveChangesAsync(cancellationToken);
        return lotId;
    }

    // A car session holding a slot of a zone without sensors, given out at the moment the test needs.
    private static async Task<(Guid SlotId, Guid SessionId)> SeedAssignedSlotAsync(
        ServiceProvider serviceProvider, Guid lotId, string slotCode, DateTimeOffset assignedAt, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var floor = Floor.Create(Guid.NewGuid(), lotId, "Tầng " + slotCode);
        var zone = Zone.Create(Guid.NewGuid(), floor.Id, "Z-" + slotCode, VehicleType.Car, 1, hasSlotSensor: false);
        var slot = ParkingSlot.Create(Guid.NewGuid(), lotId, zone.Id, slotCode);
        var session = ParkingSession.Open(
            Guid.NewGuid(), lotId, "51A-" + slotCode, VehicleType.Car, assignedAt, Guid.NewGuid().ToString("N"), null, null, zone.Id);
        slot.AssignSession(session.Id, assignedAt);
        dbContext.Floors.Add(floor);
        dbContext.Zones.Add(zone);
        dbContext.ParkingSessions.Add(session);
        dbContext.ParkingSlots.Add(slot);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (slot.Id, session.Id);
    }

    private async Task<ServiceProvider> CreateMigratedServiceProviderAsync(CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        services.AddApplicationServices();
        services.AddSingleton<IClock, SystemClock>();
        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(cancellationToken);
        return serviceProvider;
    }
}
