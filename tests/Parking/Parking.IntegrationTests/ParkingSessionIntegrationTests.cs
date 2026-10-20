using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Application.ParkingSessions.CancelParkingSession;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class ParkingSessionIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private const string Plate = "51A-123.45";
    private static readonly Guid StaffId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Unique_index_blocks_a_second_open_session_for_the_same_plate_in_the_same_lot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await CreateApprovedLotAsync(serviceProvider, cancellationToken);

        Assert.True(await SaveNewSessionAsync(serviceProvider, lotId, cancellationToken));
        Assert.False(await SaveNewSessionAsync(serviceProvider, lotId, cancellationToken));

        // The same plate may still enter another lot.
        Assert.True(await SaveNewSessionAsync(serviceProvider, await CreateApprovedLotAsync(serviceProvider, cancellationToken), cancellationToken));

        await using var scope = serviceProvider.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IParkingSessionRepository>();
        Assert.True(await sessions.HasOpenSessionAsync(lotId, Plate, cancellationToken));
    }

    [Fact]
    public async Task Plate_can_open_a_new_session_after_the_previous_one_is_completed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await CreateApprovedLotAsync(serviceProvider, cancellationToken);
        Assert.True(await SaveNewSessionAsync(serviceProvider, lotId, cancellationToken));

        // Completing a session belongs to the exit story; it is simulated here with SQL.
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE parking.parking_sessions SET status = {nameof(ParkingSessionStatus.Completed)} WHERE lot_id = {lotId}",
                cancellationToken);
        }

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<IParkingSessionRepository>();
            Assert.False(await sessions.HasOpenSessionAsync(lotId, Plate, cancellationToken));
        }

        Assert.True(await SaveNewSessionAsync(serviceProvider, lotId, cancellationToken));
    }

    [Fact]
    public async Task Cancelling_saves_status_and_audit_entry_together_and_frees_the_plate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await CreateApprovedLotAsync(serviceProvider, cancellationToken);
        Assert.True(await SaveNewSessionAsync(serviceProvider, lotId, cancellationToken));

        Guid sessionId;
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            sessionId = await dbContext.ParkingSessions.Where(session => session.LotId == lotId).Select(session => session.Id).SingleAsync(cancellationToken);
            // TECH-12: nhân viên phải được gắn với bãi mới huỷ được phiên.
            await AssignStaffAsync(serviceProvider, lotId, cancellationToken);
            var handler = new CancelParkingSessionCommandHandler(
                scope.ServiceProvider.GetRequiredService<IParkingSessionRepository>(),
                scope.ServiceProvider.GetRequiredService<IParkingLotAccess>(),
                scope.ServiceProvider.GetRequiredService<IAuditLog>(),
                scope.ServiceProvider.GetRequiredService<IClock>(),
                scope.ServiceProvider.GetRequiredService<ISlotDesignator>());

            var result = await handler.Handle(new CancelParkingSessionCommand(sessionId, "Vehicle did not enter", StaffId), cancellationToken);

            Assert.True(result.IsSuccess);
        }

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            var session = await dbContext.ParkingSessions.AsNoTracking().SingleAsync(session => session.Id == sessionId, cancellationToken);
            Assert.Equal(ParkingSessionStatus.Cancelled, session.Status);
            Assert.Equal(StaffId, session.CancelledBy);

            var entry = await dbContext.AuditLogEntries.AsNoTracking().SingleAsync(entry => entry.TargetId == sessionId.ToString(), cancellationToken);
            Assert.Equal(AuditActions.SessionCancelled, entry.Action);
            Assert.Equal(lotId, entry.LotId);
            Assert.Equal("Vehicle did not enter", entry.Reason);
        }

        // A cancelled session no longer holds the plate, so the vehicle can enter again.
        Assert.True(await SaveNewSessionAsync(serviceProvider, lotId, cancellationToken));
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

    [Fact]
    public async Task Foreign_key_rejects_a_session_for_a_lot_that_does_not_exist()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);

        await Assert.ThrowsAsync<DbUpdateException>(() => SaveNewSessionAsync(serviceProvider, Guid.NewGuid(), cancellationToken));
    }

    // parking_sessions.lot_id references parking_lots, so every session test needs a real lot row.
    private static async Task<Guid> CreateApprovedLotAsync(ServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var lotId = Guid.NewGuid();
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
            VALUES ({lotId}, 'Session Test Parking', '1 Demo Street', 10.776530, 106.700981, 'Outdoor', 'Approved')
            """,
            cancellationToken);
        return lotId;
    }

    private static async Task AssignStaffAsync(ServiceProvider serviceProvider, Guid lotId, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var assignments = scope.ServiceProvider.GetRequiredService<IParkingLotAssignmentRepository>();
        assignments.Add(ParkingLotAssignment.Create(lotId, StaffId, ParkingLotMemberRole.Staff, Guid.NewGuid(), DateTimeOffset.UtcNow));
        await assignments.SaveChangesAsync(cancellationToken);
    }

    private static async Task<bool> SaveNewSessionAsync(ServiceProvider serviceProvider, Guid lotId, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IParkingSessionRepository>();
        sessions.Add(ParkingSession.Open(
            Guid.NewGuid(),
            lotId,
            Plate,
            VehicleType.Car,
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString("N"),
            userId: null,
            vehicleId: null));
        return await sessions.TrySaveChangesAsync(cancellationToken);
    }
}
