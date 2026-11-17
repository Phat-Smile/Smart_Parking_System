using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.Common.Booking;
using ParkingSystem.Parking.Application.Common.Identity;
using ParkingSystem.Parking.Application.Common.Persistence;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions.CompleteParkingSession;
using ParkingSystem.Parking.Application.ParkingSessions.MarkParkingSessionPaid;
using ParkingSystem.Parking.Application.ParkingSessions.OpenParkingSession;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// US-PAY-006 on real PostgreSQL: paying, then leaving, with the columns, the release and the Outbox row in one commit.
public sealed class ExitPaymentIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private const string SessionClosedEvent = "parking.session-closed.v1";
    private static readonly Guid StaffId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    [Fact]
    public async Task A_paid_car_leaves_and_its_slot_is_free_again()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedLotAsync(serviceProvider, VehicleType.Car, cancellationToken);
        var opened = await OpenAsync(serviceProvider, lotId, "51A-123.45", VehicleType.Car, cancellationToken);
        Assert.NotNull(opened.SlotCode);
        var paidAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        Assert.True((await MarkPaidAsync(serviceProvider, opened.SessionId, paidAt, cancellationToken)).IsSuccess);

        var result = await CompleteAsync(serviceProvider, opened, cancellationToken);

        Assert.True(result.IsSuccess);
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var session = await dbContext.ParkingSessions.AsNoTracking().SingleAsync(item => item.Id == opened.SessionId, cancellationToken);
        Assert.Equal(ParkingSessionStatus.Completed, session.Status);
        Assert.NotNull(session.ExitTime);
        Assert.Equal(paidAt.ToUnixTimeMilliseconds(), session.LastPaidAt!.Value.ToUnixTimeMilliseconds());
        Assert.False(await dbContext.ParkingSlots.AnyAsync(slot => slot.AssignedSessionId == opened.SessionId, cancellationToken));
        Assert.Equal(1, await CountClosedEventsAsync(dbContext, opened.SessionId, cancellationToken));
    }

    [Fact]
    public async Task Two_calls_at_once_give_back_only_one_motorbike_space()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedLotAsync(serviceProvider, VehicleType.Motorbike, cancellationToken);
        // A second motorbike stays inside: a double release would drop the count to 0.
        await OpenAsync(serviceProvider, lotId, "59X1-111.11", VehicleType.Motorbike, cancellationToken);
        var opened = await OpenAsync(serviceProvider, lotId, "59X1-222.22", VehicleType.Motorbike, cancellationToken);
        await MarkPaidAsync(serviceProvider, opened.SessionId, DateTimeOffset.UtcNow, cancellationToken);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => CompleteAsync(serviceProvider, opened, cancellationToken)));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        // All report the first exit time; PostgreSQL keeps microseconds, so a repeated call can differ below that.
        var completedAt = results.Select(result => result.Value!.CompletedAt).ToList();
        Assert.True(completedAt.Max() - completedAt.Min() < TimeSpan.FromMilliseconds(1));
        await using var scope = serviceProvider.CreateAsyncScope();
        var availability = await scope.ServiceProvider.GetRequiredService<IMotorbikeZoneCounter>().GetAvailabilityAsync(lotId, cancellationToken);
        Assert.Equal(1, availability!.Occupied);
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        Assert.Equal(1, await CountClosedEventsAsync(dbContext, opened.SessionId, cancellationToken));
    }

    [Fact]
    public async Task Late_at_the_gate_is_saved_as_active_and_paying_again_lets_the_vehicle_leave()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedLotAsync(serviceProvider, VehicleType.Car, cancellationToken);
        var opened = await OpenAsync(serviceProvider, lotId, "51A-333.33", VehicleType.Car, cancellationToken);
        await MarkPaidAsync(serviceProvider, opened.SessionId, DateTimeOffset.UtcNow.AddMinutes(-20), cancellationToken);

        var late = await CompleteAsync(serviceProvider, opened, cancellationToken);

        Assert.Equal(CompleteParkingSessionErrors.ExitWindowExpired, late.Error);
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var session = await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().ParkingSessions.AsNoTracking()
                .SingleAsync(item => item.Id == opened.SessionId, cancellationToken);
            Assert.Equal(ParkingSessionStatus.Active, session.Status);
            Assert.Null(session.ExitTime);
        }

        await MarkPaidAsync(serviceProvider, opened.SessionId, DateTimeOffset.UtcNow, cancellationToken);
        Assert.True((await CompleteAsync(serviceProvider, opened, cancellationToken)).IsSuccess);
    }

    [Fact]
    public async Task An_unpaid_session_stays_open_and_keeps_its_slot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedLotAsync(serviceProvider, VehicleType.Car, cancellationToken);
        var opened = await OpenAsync(serviceProvider, lotId, "51A-444.44", VehicleType.Car, cancellationToken);

        var result = await CompleteAsync(serviceProvider, opened, cancellationToken);

        Assert.Equal(CompleteParkingSessionErrors.PaymentRequired, result.Error);
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        Assert.True(await dbContext.ParkingSlots.AnyAsync(slot => slot.AssignedSessionId == opened.SessionId, cancellationToken));
        Assert.Equal(0, await CountClosedEventsAsync(dbContext, opened.SessionId, cancellationToken));
    }

    // Payload is jsonb, so it is compared here rather than in the query.
    private static async Task<int> CountClosedEventsAsync(ParkingDbContext dbContext, Guid sessionId, CancellationToken cancellationToken)
    {
        var payloads = await dbContext.OutboxMessages.AsNoTracking()
            .Where(message => message.EventType == SessionClosedEvent)
            .Select(message => message.Payload)
            .ToListAsync(cancellationToken);
        return payloads.Count(payload => payload.Contains(sessionId.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<OpenedParkingSession> OpenAsync(
        ServiceProvider serviceProvider, Guid lotId, string plate, VehicleType vehicleType, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new OpenParkingSessionCommand(lotId, plate, vehicleType.ToString(), StaffId), cancellationToken);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static async Task<Result<ParkingSessionPaid>> MarkPaidAsync(
        ServiceProvider serviceProvider, Guid sessionId, DateTimeOffset paidAt, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new MarkParkingSessionPaidCommand(sessionId, paidAt), cancellationToken);
    }

    private static async Task<Result<CompletedParkingSession>> CompleteAsync(
        ServiceProvider serviceProvider, OpenedParkingSession opened, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new CompleteParkingSessionCommand(opened.SessionId, opened.PlateNumber, opened.QrToken, StaffId), cancellationToken);
    }

    // An approved lot with the staff member assigned (TECH-12) and one zone: two car slots, or two motorbike spaces.
    private static async Task<Guid> SeedLotAsync(ServiceProvider serviceProvider, VehicleType vehicleType, CancellationToken cancellationToken)
    {
        var lotId = Guid.NewGuid();
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
            VALUES ({lotId}, 'Exit Test Parking', '1 Demo Street', 10.776530, 106.700981, 'Outdoor', 'Approved')
            """,
            cancellationToken);

        var floor = Floor.Create(Guid.NewGuid(), lotId, "Tầng 1");
        var zone = Zone.Create(Guid.NewGuid(), floor.Id, "Z1", vehicleType, 2, hasSlotSensor: false);
        dbContext.Floors.Add(floor);
        dbContext.Zones.Add(zone);
        if (vehicleType == VehicleType.Car)
        {
            dbContext.ParkingSlots.Add(ParkingSlot.Create(Guid.NewGuid(), lotId, zone.Id, "A-01"));
            dbContext.ParkingSlots.Add(ParkingSlot.Create(Guid.NewGuid(), lotId, zone.Id, "A-02"));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

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
        services.AddSingleton<IBookingCheckInClient, NoBookingCheckInClient>();
        services.AddSingleton<IBookingNoSlotReporter, NoMatchingBookingCheckInClient>();
        services.AddSingleton<IHeldQuotaReader, NoHeldQuota>();
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

    private sealed class NoHeldQuota : IHeldQuotaReader
    {
        public Task<int> CountHeldReservationsAsync(Guid lotId, CancellationToken cancellationToken) => Task.FromResult(0);
    }

    private sealed class NoPlateOwner : IPlateOwnerLookup
    {
        public Task<PlateOwner?> FindByPlateAsync(string normalizedPlate, CancellationToken cancellationToken) => Task.FromResult<PlateOwner?>(null);
    }
}
