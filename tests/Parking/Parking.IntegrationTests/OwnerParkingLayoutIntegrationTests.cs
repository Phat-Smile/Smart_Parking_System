using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingLayout;
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

    [Fact]
    public async Task Approved_lot_put_creates_pending_review_without_changing_active_layout()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var (lot, owner) = await CreateLotAndOwnerAsync(provider, ct);
        var floor = new LayoutFloorInput(Guid.NewGuid(), "Original", []);
        Assert.True((await SaveAsync(provider, lot, owner, 0, [floor], ct)).IsSuccess);
        await ApproveLotAsync(provider, lot, ct);

        var proposed = floor with { Name = "After review" };
        var submitted = await SaveAsync(provider, lot, owner, 1, [proposed], ct);
        Assert.True(submitted.IsSuccess, submitted.Error?.Message);
        Assert.Equal(1, submitted.Value!.Version);
        var review = Assert.IsType<LayoutReviewSubmission>(submitted.Value.PendingReview);
        Assert.Equal("Pending", review.Status);
        Assert.Equal("Original", Assert.Single(submitted.Value.Floors).Name);
        Assert.Empty(submitted.Value.Warnings);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        Assert.Equal(1, (await db.LayoutDocuments.AsNoTracking().SingleAsync(d => d.LotId == lot, ct)).Version);
        Assert.Equal("Original", (await db.Floors.AsNoTracking().SingleAsync(f => f.LotId == lot, ct)).Name);
        Assert.Equal(ParkingLayoutChangeStatus.Pending,
            (await db.LayoutChangeRequests.AsNoTracking().SingleAsync(r => r.Id == review.RequestId, ct)).Status);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Assert.Contains(await sender.Send(new ListPendingLayoutReviewsQuery(), ct), r =>
            r.RequestId == review.RequestId && Assert.Single(r.ProposedFloors).Name == "After review");
        var duplicate = await SaveAsync(provider, lot, owner, 1, [proposed], ct);
        Assert.Equal(LayoutReviewErrors.PendingAlreadyExists, duplicate.Error);
    }

    [Fact]
    public async Task Admin_approve_applies_layout_and_request_status_together()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var (lot, owner) = await CreateLotAndOwnerAsync(provider, ct);
        var floor = new LayoutFloorInput(Guid.NewGuid(), "Original", []);
        Assert.True((await SaveAsync(provider, lot, owner, 0, [floor], ct)).IsSuccess);
        await ApproveLotAsync(provider, lot, ct);
        var submission = await SaveAsync(provider, lot, owner, 1, [floor with { Name = "Renamed" }], ct);
        var id = submission.Value!.PendingReview!.RequestId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.Send(new ReviewLayoutChangeCommand(lot, id, Guid.NewGuid(), true, null), ct);
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal("Approved", result.Value!.Request.Status);
            Assert.Equal(2, result.Value.ActiveLayout!.Version);
            Assert.Equal("Renamed", Assert.Single(result.Value.ActiveLayout.Floors).Name);
        }
        await using var verification = provider.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<ParkingDbContext>();
        Assert.Equal(ParkingLayoutChangeStatus.Approved,
            (await db.LayoutChangeRequests.AsNoTracking().SingleAsync(r => r.Id == id, ct)).Status);
        Assert.Equal(2, (await db.LayoutDocuments.AsNoTracking().SingleAsync(d => d.LotId == lot, ct)).Version);
        Assert.DoesNotContain(await verification.ServiceProvider.GetRequiredService<ISender>()
            .Send(new ListPendingLayoutReviewsQuery(), ct), r => r.RequestId == id);
    }

    [Fact]
    public async Task Admin_reject_keeps_active_layout_and_second_review_conflicts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var (lot, owner) = await CreateLotAndOwnerAsync(provider, ct);
        var floor = new LayoutFloorInput(Guid.NewGuid(), "Ground", []);
        Assert.True((await SaveAsync(provider, lot, owner, 0, [floor], ct)).IsSuccess);
        await ApproveLotAsync(provider, lot, ct);
        var proposal = await SaveAsync(provider, lot, owner, 1, [floor with { Name = "Unapproved" }], ct);
        var requestId = proposal.Value!.PendingReview!.RequestId;
        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var denied = await sender.Send(new ReviewLayoutChangeCommand(lot, requestId, Guid.NewGuid(), false, "No"), ct);
        Assert.True(denied.IsSuccess, denied.Error?.Message);
        Assert.Null(denied.Value!.ActiveLayout);
        var again = await sender.Send(new ReviewLayoutChangeCommand(lot, requestId, Guid.NewGuid(), true, null), ct);
        Assert.Equal(LayoutReviewErrors.AlreadyReviewed, again.Error);
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        Assert.Equal("Ground", (await db.Floors.AsNoTracking().SingleAsync(f => f.LotId == lot, ct)).Name);
        Assert.Equal(1, (await db.LayoutDocuments.AsNoTracking().SingleAsync(d => d.LotId == lot, ct)).Version);
    }

    [Fact]
    public async Task Stale_version_on_review_does_not_approve_or_change_operational_layout()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var (lot, owner) = await CreateLotAndOwnerAsync(provider, ct);
        var floor = new LayoutFloorInput(Guid.NewGuid(), "Ground", []);
        Assert.True((await SaveAsync(provider, lot, owner, 0, [floor], ct)).IsSuccess);
        await ApproveLotAsync(provider, lot, ct);
        var request = await SaveAsync(provider, lot, owner, 1, [floor with { Name = "Proposed" }], ct);
        var requestId = request.Value!.PendingReview!.RequestId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE parking.parking_layout_documents SET version = 2 WHERE lot_id = {lot}", ct);
        }
        await using var reviewScope = provider.CreateAsyncScope();
        var sender = reviewScope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new ReviewLayoutChangeCommand(lot, requestId, Guid.NewGuid(), true, null), ct);
        Assert.Equal(ParkingLayoutErrors.StaleVersion, result.Error);
        var verify = reviewScope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        Assert.Equal(ParkingLayoutChangeStatus.Pending,
            (await verify.LayoutChangeRequests.AsNoTracking().SingleAsync(r => r.Id == requestId, ct)).Status);
        Assert.Equal("Ground", (await verify.Floors.AsNoTracking().SingleAsync(f => f.LotId == lot, ct)).Name);
    }

    [Fact]
    public async Task Invalid_operational_edit_at_review_keeps_request_pending_and_data_unchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var (lot, owner) = await CreateLotAndOwnerAsync(provider, ct);
        var slot = new LayoutSlotInput(Guid.NewGuid(), "C-01", null, new GridPoint(0, 0));
        var zone = new LayoutZoneInput(Guid.NewGuid(), "C", "Car", 2, false, [slot]);
        var floor = new LayoutFloorInput(Guid.NewGuid(), "Ground", [zone]);
        Assert.True((await SaveAsync(provider, lot, owner, 0, [floor], ct)).IsSuccess);
        await ApproveLotAsync(provider, lot, ct);
        // This is syntactically valid, but removing an existing slot is forbidden during apply.
        var removed = floor with { Zones = [zone with { Slots = [] }] };
        var request = await SaveAsync(provider, lot, owner, 1, [removed], ct);
        Assert.True(request.IsSuccess, request.Error?.Message);
        var id = request.Value!.PendingReview!.RequestId;
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new ReviewLayoutChangeCommand(lot, id, Guid.NewGuid(), true, null), ct);
        Assert.Equal(ParkingLayoutErrors.Conflict, result.Error);
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        Assert.Equal(ParkingLayoutChangeStatus.Pending,
            (await db.LayoutChangeRequests.AsNoTracking().SingleAsync(r => r.Id == id, ct)).Status);
        Assert.Equal(1, await db.ParkingSlots.AsNoTracking().CountAsync(s => s.LotId == lot, ct));
        Assert.Equal(1, (await db.LayoutDocuments.AsNoTracking().SingleAsync(d => d.LotId == lot, ct)).Version);
    }

    [Fact]
    public async Task Database_failure_while_marking_approved_rolls_back_active_layout_changes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var (lot, owner) = await CreateLotAndOwnerAsync(provider, ct);
        var floor = new LayoutFloorInput(Guid.NewGuid(), "Before", []);
        Assert.True((await SaveAsync(provider, lot, owner, 0, [floor], ct)).IsSuccess);
        await ApproveLotAsync(provider, lot, ct);
        var proposal = await SaveAsync(provider, lot, owner, 1, [floor with { Name = "After" }], ct);
        var id = proposal.Value!.PendingReview!.RequestId;

        // Force the *second* SaveChanges (status=Approved) to fail after SaveActiveAsync saves.
        // The per-request check is cleaned up; no shared fixtures or other requests are changed.
        const string constraint = "ck_test_layout_review_atomicity";
        var guard = $"ALTER TABLE parking.parking_layout_change_requests ADD CONSTRAINT {constraint} " +
            $"CHECK (id <> '{id:D}'::uuid OR status <> 'Approved')";
        await using (var setup = provider.CreateAsyncScope())
        {
            await setup.ServiceProvider.GetRequiredService<ParkingDbContext>()
                .Database.ExecuteSqlRawAsync(guard, ct);
        }
        try
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await Assert.ThrowsAsync<DbUpdateException>(async () =>
                {
                    await sender.Send(new ReviewLayoutChangeCommand(lot, id, Guid.NewGuid(), true, null), ct);
                });
            }
            await using var verification = provider.CreateAsyncScope();
            var db = verification.ServiceProvider.GetRequiredService<ParkingDbContext>();
            Assert.Equal("Before", (await db.Floors.AsNoTracking().SingleAsync(f => f.LotId == lot, ct)).Name);
            Assert.Equal(1, (await db.LayoutDocuments.AsNoTracking().SingleAsync(d => d.LotId == lot, ct)).Version);
            Assert.Equal(ParkingLayoutChangeStatus.Pending,
                (await db.LayoutChangeRequests.AsNoTracking().SingleAsync(r => r.Id == id, ct)).Status);
        }
        finally
        {
            await using var cleanup = provider.CreateAsyncScope();
            await cleanup.ServiceProvider.GetRequiredService<ParkingDbContext>()
                .Database.ExecuteSqlRawAsync($"ALTER TABLE parking.parking_layout_change_requests DROP CONSTRAINT {constraint}", CancellationToken.None);
        }
    }

    private static async Task ApproveLotAsync(ServiceProvider provider, Guid lot, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE parking.parking_lots SET approval_status = 'Approved' WHERE id = {lot}", ct);
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
