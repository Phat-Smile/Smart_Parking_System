using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.GateReadings;
using ParkingSystem.Parking.Application.GateReadings.ConfirmGateReading;
using ParkingSystem.Parking.Application.GateReadings.ListPendingGateReadings;
using ParkingSystem.Parking.Application.GateReadings.RecordGateReading;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.GateReadings;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// US-PARK-001 on real PostgreSQL: a reading is stored with its decision and read back unchanged.
public sealed class GateReadingIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly Guid StaffId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    [Fact]
    public async Task Readings_are_stored_with_their_decision_confidence_and_plates()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedAssignedApprovedLotAsync(serviceProvider, cancellationToken);

        var accepted = await RecordAsync(serviceProvider, new RecordGateReadingCommand(lotId, "51a-123.45", "Car", 0.925m, StaffId), cancellationToken);
        var pending = await RecordAsync(serviceProvider, new RecordGateReadingCommand(lotId, null, "Motorbike", 0.2m, StaffId), cancellationToken);

        await using var scope = serviceProvider.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().GateReadings.AsNoTracking()
            .Where(reading => reading.LotId == lotId)
            .ToDictionaryAsync(reading => reading.Id, cancellationToken);

        var first = stored[accepted.ReadingId];
        Assert.Equal(GateReadingDecision.AutoAccepted, first.Decision);
        Assert.Equal("51a-123.45", first.RawPlate);
        Assert.Equal("51A-123.45", first.PlateNumber);
        Assert.Equal(0.925m, first.Confidence);
        Assert.Equal(VehicleType.Car, first.VehicleType);
        Assert.Equal(StaffId, first.RecordedBy);

        var second = stored[pending.ReadingId];
        Assert.Equal(GateReadingDecision.PendingStaff, second.Decision);
        Assert.Equal(string.Empty, second.PlateNumber);
        Assert.Equal(VehicleType.Motorbike, second.VehicleType);
    }

    [Fact]
    public async Task A_reading_for_a_lot_that_does_not_exist_cannot_be_stored()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        dbContext.GateReadings.Add(GateReading.Record(Guid.NewGuid(), Guid.NewGuid(), "51A-123.45", VehicleType.Car, 0.95m, DateTimeOffset.UtcNow, StaffId));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync(cancellationToken));
    }

    [Fact]
    public async Task Two_staff_confirming_the_same_reading_at_once_only_one_wins_and_one_audit_entry_is_written()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedAssignedApprovedLotAsync(serviceProvider, cancellationToken);
        var reading = await RecordAsync(serviceProvider, new RecordGateReadingCommand(lotId, "51A-123.45", "Car", 0.4m, StaffId), cancellationToken);

        // Both staff load the reading while it is still PendingStaff, then save one after the other: the worst interleaving,
        // forced here so the test does not depend on thread timing.
        await using var first = serviceProvider.CreateAsyncScope();
        await using var second = serviceProvider.CreateAsyncScope();
        var firstStaff = await LoadAndConfirmAsync(first, reading.ReadingId, "51A-123.45", cancellationToken);
        var secondStaff = await LoadAndConfirmAsync(second, reading.ReadingId, "51A-123.46", cancellationToken);

        Assert.True(await firstStaff.TrySaveChangesAsync(cancellationToken));
        Assert.False(await secondStaff.TrySaveChangesAsync(cancellationToken));

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var stored = await dbContext.GateReadings.AsNoTracking().SingleAsync(item => item.Id == reading.ReadingId, cancellationToken);
        Assert.Equal(GateReadingDecision.StaffConfirmed, stored.Decision);
        Assert.Equal("51A-123.45", stored.PlateNumber);
        Assert.Equal(StaffId, stored.ConfirmedBy);
        Assert.NotNull(stored.ConfirmedAt);
        // The losing save is rolled back with its audit entry.
        Assert.Equal(1, await dbContext.AuditLogEntries.CountAsync(
            entry => entry.TargetId == reading.ReadingId.ToString() && entry.Action == AuditActions.GateReadingConfirmed, cancellationToken));

        // Through the handler, the second staff member then gets 409 not pending.
        Assert.Equal(ConfirmGateReadingErrors.NotPending, (await ConfirmAsync(serviceProvider, reading.ReadingId, "51A-123.46", cancellationToken)).Error);
    }

    // What the confirm handler does before saving: load, confirm, add the audit entry.
    private static async Task<IGateReadingRepository> LoadAndConfirmAsync(
        AsyncServiceScope scope, Guid readingId, string plate, CancellationToken cancellationToken)
    {
        var readings = scope.ServiceProvider.GetRequiredService<IGateReadingRepository>();
        var reading = await readings.FindByIdAsync(readingId, cancellationToken);
        Assert.True(reading!.Confirm(plate, VehicleType.Car, StaffId, DateTimeOffset.UtcNow));
        scope.ServiceProvider.GetRequiredService<IAuditLog>().Record(new AuditEntry(
            AuditActions.GateReadingConfirmed, StaffId, reading.LotId, nameof(GateReading), reading.Id.ToString(), null, DateTimeOffset.UtcNow));
        return readings;
    }

    [Fact]
    public async Task Pending_queue_lists_only_the_readings_waiting_for_staff_oldest_first()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedAssignedApprovedLotAsync(serviceProvider, cancellationToken);
        var first = await RecordAsync(serviceProvider, new RecordGateReadingCommand(lotId, "51A-111.11", "Car", 0.3m, StaffId), cancellationToken);
        await RecordAsync(serviceProvider, new RecordGateReadingCommand(lotId, "51A-222.22", "Car", 0.99m, StaffId), cancellationToken);
        var second = await RecordAsync(serviceProvider, new RecordGateReadingCommand(lotId, null, "Motorbike", 0.1m, StaffId), cancellationToken);
        var confirmed = await RecordAsync(serviceProvider, new RecordGateReadingCommand(lotId, "51A-333.33", "Car", 0.2m, StaffId), cancellationToken);
        Assert.True((await ConfirmAsync(serviceProvider, confirmed.ReadingId, "51A-333.33", cancellationToken)).IsSuccess);

        await using var scope = serviceProvider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ListPendingGateReadingsQuery(lotId, StaffId), cancellationToken);

        Assert.Equal([first.ReadingId, second.ReadingId], result.Value!.Select(item => item.ReadingId));
    }

    private static async Task<Result<ConfirmedGateReading>> ConfirmAsync(
        ServiceProvider serviceProvider, Guid readingId, string plate, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new ConfirmGateReadingCommand(readingId, plate, "Car", StaffId), cancellationToken);
    }

    private static async Task<RecordedGateReading> RecordAsync(ServiceProvider serviceProvider, RecordGateReadingCommand command, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, cancellationToken);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static async Task<Guid> SeedAssignedApprovedLotAsync(ServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var lotId = Guid.NewGuid();
        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
            VALUES ({lotId}, 'Gate Reading Test Parking', '1 Demo Street', 10.776530, 106.700981, 'Outdoor', 'Approved')
            """,
            cancellationToken);

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
        var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(cancellationToken);
        return serviceProvider;
    }
}
