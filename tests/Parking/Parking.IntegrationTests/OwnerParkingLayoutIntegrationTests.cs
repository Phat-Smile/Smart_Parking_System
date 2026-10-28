using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class OwnerParkingLayoutIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Assigned_owner_can_save_read_and_add_items_without_losing_ids()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var (lotId, ownerId) = await CreateLotAndOwnerAsync(provider, ct);
        var floorId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var layout = new[] { new LayoutFloorInput(floorId, "Ground", [
            new LayoutZoneInput(zoneId, "A", "Car", 3, false, [new LayoutSlotInput(slotId, "a-1", null, new GridPoint(0, 0))])
        ]) };

        var first = await SaveAsync(provider, lotId, ownerId, 0, layout, ct);
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(1, first.Value!.Version);
        Assert.Equal(new GridPoint(0, 0), first.Value!.Floors.Single().Zones.Single().Slots.Single().Position);
        Assert.Equal("A-1", first.Value!.Floors.Single().Zones.Single().Slots.Single().SlotCode);
        var updated = new[] { layout[0] with { Name = "Ground floor", Zones = [
            layout[0].Zones![0] with { Slots = [layout[0].Zones![0].Slots![0], new LayoutSlotInput(Guid.NewGuid(), "a-2", "Unavailable", new GridPoint(1, 0))] }
        ] } };
        var second = await SaveAsync(provider, lotId, ownerId, first.Value!.Version, updated, ct);
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(2, second.Value!.Version);
        Assert.Equal(floorId, second.Value!.Floors.Single().Id);
        Assert.Equal(2, second.Value.Floors.Single().Zones.Single().Slots.Count);
        var third = await SaveAsync(provider, lotId, ownerId, second.Value!.Version, updated, ct);
        Assert.True(third.IsSuccess, third.Error?.Message);
        Assert.Equal(2, third.Value!.Version);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        Assert.Equal(2, await db.ParkingSlots.CountAsync(s => s.LotId == lotId, ct));
        Assert.Equal(2, await db.AuditLogEntries.CountAsync(a => a.LotId == lotId && a.Action == AuditActions.ParkingLayoutUpdated, ct));
    }

    [Fact]
    public async Task Staff_and_owner_of_another_lot_cannot_save_or_read()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var (lotId, ownerId) = await CreateLotAndOwnerAsync(provider, ct);
        var otherLot = await CreateLotAsync(provider, ct);
        var staffId = Guid.NewGuid();
        await AssignAsync(provider, lotId, staffId, ParkingLotMemberRole.Staff, ct);
        Assert.Equal(ParkingLayoutErrors.OwnerRequired, (await SaveAsync(provider, lotId, staffId, 0, [], ct)).Error);
        Assert.Equal(ParkingLayoutErrors.OwnerRequired, (await SaveAsync(provider, otherLot, ownerId, 0, [], ct)).Error);
        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ISender>();
        var read = await handler.Send(new GetParkingLayoutQuery(otherLot, ownerId), ct);
        Assert.Equal(ParkingLayoutErrors.OwnerRequired, read.Error);
    }

    [Fact]
    public async Task Omitting_existing_slots_is_conflict_and_keeps_database_unchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var (lotId, ownerId) = await CreateLotAndOwnerAsync(provider, ct);
        var zone = new LayoutZoneInput(Guid.NewGuid(), "A", "Car", 1, false, [new LayoutSlotInput(Guid.NewGuid(), "A1", null, new GridPoint(0, 0))]);
        var floor = new LayoutFloorInput(Guid.NewGuid(), "Ground", [zone]);
        Assert.True((await SaveAsync(provider, lotId, ownerId, 0, [floor], ct)).IsSuccess);
        var rejected = await SaveAsync(provider, lotId, ownerId, 1, [floor with { Zones = [zone with { Slots = [] }] }], ct);
        Assert.Equal(ParkingLayoutErrors.Conflict, rejected.Error);
        await using var scope = provider.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().ParkingSlots.CountAsync(s => s.LotId == lotId, ct));
    }

    [Fact]
    public async Task Sensor_occupied_slot_cannot_be_moved_and_its_state_is_not_reset()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var (lotId, ownerId) = await CreateLotAndOwnerAsync(provider, ct);
        var slot = new LayoutSlotInput(Guid.NewGuid(), "CAR1", null, new GridPoint(1, 1));
        var floor = new LayoutFloorInput(Guid.NewGuid(), "Ground", [
            new LayoutZoneInput(Guid.NewGuid(), "CAR", "Car", 2, true, [slot])]);
        Assert.True((await SaveAsync(provider, lotId, ownerId, 0, [floor], ct)).IsSuccess);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE parking.parking_slots SET sensor_state = 'Occupied', effective_status = 'Occupied', row_version = row_version + 1 WHERE id = {slot.Id}""", ct);
        }
        var moved = floor with { Zones = [floor.Zones![0] with { Slots = [slot with { Position = new GridPoint(2, 1) }] }] };
        Assert.Equal(ParkingLayoutErrors.Conflict, (await SaveAsync(provider, lotId, ownerId, 1, [moved], ct)).Error);
        await using var readScope = provider.CreateAsyncScope();
        var state = await readScope.ServiceProvider.GetRequiredService<ParkingDbContext>()
            .ParkingSlots.AsNoTracking().Where(s => s.Id == slot.Id).Select(s => s.EffectiveStatus).SingleAsync(ct);
        Assert.Equal(ParkingSystem.Parking.Domain.ParkingLayout.SlotEffectiveStatus.Occupied, state);
    }

    [Fact]
    public async Task Stale_layout_version_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var (lotId, ownerId) = await CreateLotAndOwnerAsync(provider, ct);
        var floor = new LayoutFloorInput(Guid.NewGuid(), "Ground", []);
        Assert.True((await SaveAsync(provider, lotId, ownerId, 0, [floor], ct)).IsSuccess);
        var stale = await SaveAsync(provider, lotId, ownerId, 0, [floor with { Name = "Changed" }], ct);
        Assert.Equal(ParkingLayoutErrors.StaleVersion, stale.Error);
    }

    private async Task<ServiceProvider> CreateProviderAsync(CancellationToken ct)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(ct);
        return provider;
    }

    private static async Task<Guid> CreateLotAsync(ServiceProvider provider, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status) VALUES ({id}, 'Layout Test', '1 Test St', 10.1, 106.1, 'Outdoor', 'Pending')""", ct);
        return id;
    }

    private static async Task<(Guid lot, Guid owner)> CreateLotAndOwnerAsync(ServiceProvider provider, CancellationToken ct)
    {
        var lot = await CreateLotAsync(provider, ct);
        var owner = Guid.NewGuid();
        await AssignAsync(provider, lot, owner, ParkingLotMemberRole.Owner, ct);
        return (lot, owner);
    }

    private static async Task AssignAsync(ServiceProvider provider, Guid lot, Guid account, ParkingLotMemberRole role, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        db.ParkingLotAssignments.Add(ParkingLotAssignment.Create(lot, account, role, Guid.NewGuid(), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    private static async Task<ParkingSystem.BuildingBlocks.Results.Result<ParkingLotLayout>> SaveAsync(
        ServiceProvider provider, Guid lot, Guid owner, int version, IReadOnlyList<LayoutFloorInput> floors, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new SaveParkingLayoutCommand(lot, owner, version, floors), ct);
    }
}
