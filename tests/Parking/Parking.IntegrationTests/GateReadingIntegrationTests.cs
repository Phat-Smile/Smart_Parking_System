using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application;
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
