using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.GateReadings.ConfirmGateReading;
using ParkingSystem.Parking.Application.GateReadings.ListPendingGateReadings;
using ParkingSystem.Parking.Application.GateReadings.RecordGateReading;
using ParkingSystem.Parking.Application.Incidents;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.Incidents;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// US-PARK-001/002 on real PostgreSQL: a reading staff must confirm is stored in parking.incidents (team ERD).
public sealed class GateReadingIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly Guid StaffId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    [Fact]
    public async Task Only_a_pending_reading_is_stored_as_an_open_low_confidence_plate_incident()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedAssignedApprovedLotAsync(serviceProvider, cancellationToken);

        var accepted = await RecordAsync(serviceProvider, new RecordGateReadingCommand(lotId, "51a-123.45", "Car", 0.925m, StaffId), cancellationToken);
        var pending = await RecordAsync(serviceProvider, new RecordGateReadingCommand(lotId, "51a-123.45", "Motorbike", 0.899m, StaffId), cancellationToken);
        var noPlate = await RecordAsync(serviceProvider, new RecordGateReadingCommand(lotId, null, "Car", 0.2m, StaffId), cancellationToken);

        Assert.Null(accepted.IncidentId);
        await using var scope = serviceProvider.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Incidents.AsNoTracking()
            .Where(incident => incident.LotId == lotId)
            .ToDictionaryAsync(incident => incident.Id, cancellationToken);
        Assert.Equal(2, stored.Count);

        var first = stored[pending.IncidentId!.Value];
        Assert.Equal(IncidentType.LowConfidencePlate, first.IncidentType);
        Assert.Equal(IncidentStatus.Open, first.Status);
        Assert.Equal("51A-123.45", first.ObservedPlate);
        Assert.Equal(0.89m, first.PlateConfidence); // numeric(3,2), rounded down
        Assert.Equal(VehicleType.Motorbike, first.ObservedVehicleType);
        Assert.Null(first.SessionId);
        Assert.Null(first.DeviceId);

        Assert.Null(stored[noPlate.IncidentId!.Value].ObservedPlate);
    }

    [Fact]
    public async Task An_incident_for_a_lot_that_does_not_exist_cannot_be_stored()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        dbContext.Incidents.Add(Incident.OpenLowConfidencePlate(Guid.NewGuid(), Guid.NewGuid(), "51A-123.45", 0.5m, VehicleType.Car, DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync(cancellationToken));
    }

    [Fact]
    public async Task Two_staff_confirming_the_same_reading_at_once_only_one_wins_and_one_audit_entry_is_written()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedAssignedApprovedLotAsync(serviceProvider, cancellationToken);
        var reading = await RecordAsync(serviceProvider, new RecordGateReadingCommand(lotId, "51A-123.45", "Car", 0.4m, StaffId), cancellationToken);
        var incidentId = reading.IncidentId!.Value;

        // Both staff load the incident while it is still Open, then save one after the other: the worst interleaving,
        // forced here so the test does not depend on thread timing.
        await using var first = serviceProvider.CreateAsyncScope();
        await using var second = serviceProvider.CreateAsyncScope();
        var firstStaff = await LoadAndResolveAsync(first, incidentId, "first", cancellationToken);
        var secondStaff = await LoadAndResolveAsync(second, incidentId, "second", cancellationToken);

        Assert.True(await firstStaff.TrySaveChangesAsync(cancellationToken));
        Assert.False(await secondStaff.TrySaveChangesAsync(cancellationToken));

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var stored = await dbContext.Incidents.AsNoTracking().SingleAsync(item => item.Id == incidentId, cancellationToken);
        Assert.Equal(IncidentStatus.Resolved, stored.Status);
        Assert.Equal("first", stored.ResolutionReason);
        Assert.Equal(StaffId, stored.ResolvedBy);
        Assert.NotNull(stored.ResolvedAt);
        // The losing save is rolled back with its audit entry.
        Assert.Equal(1, await dbContext.AuditLogEntries.CountAsync(
            entry => entry.TargetId == incidentId.ToString() && entry.Action == AuditActions.GateReadingConfirmed, cancellationToken));

        // Through the handler, the second staff member then gets 409 not pending.
        Assert.Equal(ConfirmGateReadingErrors.NotPending, (await ConfirmAsync(serviceProvider, incidentId, "51A-123.46", cancellationToken)).Error);
    }

    [Fact]
    public async Task Confirming_keeps_the_observed_plate_and_writes_the_confirmed_one_in_the_resolution_reason()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedAssignedApprovedLotAsync(serviceProvider, cancellationToken);
        var reading = await RecordAsync(serviceProvider, new RecordGateReadingCommand(lotId, "51A-123.4S", "Car", 0.95m, StaffId), cancellationToken);

        var result = await ConfirmAsync(serviceProvider, reading.IncidentId!.Value, "51A-123.45", cancellationToken);

        Assert.True(result.IsSuccess);
        await using var scope = serviceProvider.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Incidents.AsNoTracking()
            .SingleAsync(item => item.Id == reading.IncidentId, cancellationToken);
        Assert.Equal("51A-123.4S", stored.ObservedPlate);
        Assert.Contains("51A-123.45", stored.ResolutionReason);
    }

    // What the confirm handler does before saving: load, resolve, add the audit entry.
    private static async Task<IIncidentRepository> LoadAndResolveAsync(
        AsyncServiceScope scope, Guid incidentId, string reason, CancellationToken cancellationToken)
    {
        var incidents = scope.ServiceProvider.GetRequiredService<IIncidentRepository>();
        var incident = await incidents.FindByIdAsync(incidentId, cancellationToken);
        Assert.True(incident!.Resolve(StaffId, DateTimeOffset.UtcNow, reason));
        scope.ServiceProvider.GetRequiredService<IAuditLog>().Record(new AuditEntry(
            AuditActions.GateReadingConfirmed, StaffId, incident.LotId, nameof(Incident), incident.Id.ToString(), null, DateTimeOffset.UtcNow));
        return incidents;
    }

    [Fact]
    public async Task Pending_queue_lists_only_the_open_incidents_oldest_first()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = await CreateMigratedServiceProviderAsync(cancellationToken);
        var lotId = await SeedAssignedApprovedLotAsync(serviceProvider, cancellationToken);
        var first = await RecordAsync(serviceProvider, new RecordGateReadingCommand(lotId, "51A-111.11", "Car", 0.3m, StaffId), cancellationToken);
        await RecordAsync(serviceProvider, new RecordGateReadingCommand(lotId, "51A-222.22", "Car", 0.99m, StaffId), cancellationToken);
        var second = await RecordAsync(serviceProvider, new RecordGateReadingCommand(lotId, null, "Motorbike", 0.1m, StaffId), cancellationToken);
        var confirmed = await RecordAsync(serviceProvider, new RecordGateReadingCommand(lotId, "51A-333.33", "Car", 0.2m, StaffId), cancellationToken);
        Assert.True((await ConfirmAsync(serviceProvider, confirmed.IncidentId!.Value, "51A-333.33", cancellationToken)).IsSuccess);

        await using var scope = serviceProvider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ListPendingGateReadingsQuery(lotId, StaffId), cancellationToken);

        Assert.Equal([first.IncidentId!.Value, second.IncidentId!.Value], result.Value!.Select(item => item.IncidentId));
    }

    private static async Task<Result<ConfirmedGateReading>> ConfirmAsync(
        ServiceProvider serviceProvider, Guid incidentId, string plate, CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new ConfirmGateReadingCommand(incidentId, plate, "Car", StaffId), cancellationToken);
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
