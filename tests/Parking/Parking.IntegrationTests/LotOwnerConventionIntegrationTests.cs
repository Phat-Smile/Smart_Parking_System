using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;
using ParkingSystem.Parking.Application.ParkingLots.OpeningHours;
using ParkingSystem.Parking.Application.ParkingLots.RegisterParkingLot;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// MOD-PK-04 and MOD-PK-05. One definition of "owner of a lot": an assignment row with Role = Owner.
// Registering a lot creates that row for the registrant; parking_lots.owner_user_id only records who registered the lot.
// Before this change a freshly registered owner got 404 on opening hours and OwnerRequired on the layout (reproduced on 173fc80).
public sealed class LotOwnerConventionIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private const string BackfillMigrationSuffix = "_BackfillLotOwnerAssignments";

    // DDL cannot take parameters, so the account the trigger rejects is a fixed value no other test uses.
    private static readonly Guid BlockedAccount = Guid.Parse("bad00000-0000-4000-8000-000000000001");

    private const string BlockOwnerRowForTestAccount = """
        CREATE FUNCTION parking.test_reject_owner_row() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION 'owner row rejected by test';
        END
        $$;
        CREATE TRIGGER test_reject_owner_row BEFORE INSERT ON parking.parking_lot_assignments
            FOR EACH ROW WHEN (NEW.account_id = 'bad00000-0000-4000-8000-000000000001')
            EXECUTE FUNCTION parking.test_reject_owner_row();
        """;

    private const string UnblockOwnerRowForTestAccount = """
        DROP TRIGGER IF EXISTS test_reject_owner_row ON parking.parking_lot_assignments;
        DROP FUNCTION IF EXISTS parking.test_reject_owner_row();
        """;

    [Fact]
    public async Task Registering_a_lot_creates_the_owner_assignment_in_the_same_save()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var ownerId = Guid.NewGuid();

        var lotId = await RegisterAsync(provider, ownerId, "Owner Row Lot", ct);

        await using var scope = provider.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<ParkingDbContext>()
            .ParkingLotAssignments.AsNoTracking().Where(a => a.LotId == lotId).ToListAsync(ct);
        var row = Assert.Single(rows);
        Assert.Equal(ownerId, row.AccountId);
        Assert.Equal(ParkingLotMemberRole.Owner, row.Role);
        Assert.Equal(ownerId, row.AssignedBy);
        Assert.InRange(row.AssignedAtUtc, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task A_failure_while_saving_the_owner_row_leaves_no_lot_behind()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        await ExecuteAsync(provider, BlockOwnerRowForTestAccount, ct);
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => RegisterAsync(provider, BlockedAccount, "Atomic Registration Lot", ct));

            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            Assert.False(await db.ParkingLots.AnyAsync(lot => lot.OwnerUserId == BlockedAccount, ct));
        }
        finally
        {
            await ExecuteAsync(provider, UnblockOwnerRowForTestAccount, ct);
        }
    }

    [Fact]
    public async Task A_duplicate_registration_creates_neither_a_second_lot_nor_a_second_assignment()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var ownerId = Guid.NewGuid();
        await RegisterAsync(provider, ownerId, "Duplicate Owner Row Lot", ct);

        await Assert.ThrowsAsync<DuplicateParkingLotException>(() => RegisterAsync(provider, ownerId, "Duplicate Owner Row Lot", ct));

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        Assert.Equal(1, await db.ParkingLots.CountAsync(l => l.OwnerUserId == ownerId, ct));
        Assert.Equal(1, await db.ParkingLotAssignments.CountAsync(a => a.AccountId == ownerId, ct));
    }

    [Fact]
    public async Task A_freshly_registered_owner_can_configure_opening_hours()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var ownerId = Guid.NewGuid();
        var lotId = await RegisterAsync(provider, ownerId, "Fresh Owner Hours Lot", ct);

        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new ConfigureOpeningHoursCommand(lotId, ownerId, new WeeklyOpeningHours(
                Enumerable.Range(0, 7).Select(day => new DailyOpeningHours(day, true, false, null, null)).ToArray())),
            ct);

        Assert.True(result.IsSuccess, result.Error?.Code);
    }

    [Fact]
    public async Task A_freshly_registered_owner_can_save_and_read_the_layout()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var ownerId = Guid.NewGuid();
        var lotId = await RegisterAsync(provider, ownerId, "Fresh Owner Layout Lot", ct);
        var floors = new[]
        {
            new LayoutFloorInput(Guid.NewGuid(), "Ground", [
                new LayoutZoneInput(Guid.NewGuid(), "A", "Car", 2, false, [
                    new LayoutSlotInput(Guid.NewGuid(), "A-1", null, new GridPoint(0, 0)),
                    new LayoutSlotInput(Guid.NewGuid(), "A-2", null, new GridPoint(1, 0))
                ])
            ])
        };

        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var saved = await sender.Send(new SaveParkingLayoutCommand(lotId, ownerId, 0, floors), ct);
        var read = await sender.Send(new GetParkingLayoutQuery(lotId, ownerId), ct);

        Assert.True(saved.IsSuccess, saved.Error?.Message);
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal(2, read.Value!.Floors.Single().Zones.Single().Slots.Count);
    }

    [Fact]
    public async Task Only_an_owner_assignment_makes_someone_the_owner_of_a_lot()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(ct);
        var registrantId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var lotId = await RegisterAsync(provider, registrantId, "Single Source Lot", ct);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            db.ParkingLotAssignments.Add(ParkingLotAssignment.Create(lotId, staffId, ParkingLotMemberRole.Staff, registrantId, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(ct);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var ownership = scope.ServiceProvider.GetRequiredService<ILotOwnershipService>();
            Assert.True(await ownership.IsLotOwnerAsync(lotId, registrantId));
            Assert.False(await ownership.IsLotOwnerAsync(lotId, staffId));
            Assert.False(await ownership.IsLotOwnerAsync(lotId, strangerId));
        }

        // owner_user_id is only the registrant: once an admin removes the Owner row, the registrant is no longer the owner anywhere.
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            db.ParkingLotAssignments.Remove(await db.ParkingLotAssignments.SingleAsync(a => a.LotId == lotId && a.AccountId == registrantId, ct));
            await db.SaveChangesAsync(ct);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            Assert.False(await scope.ServiceProvider.GetRequiredService<ILotOwnershipService>().IsLotOwnerAsync(lotId, registrantId));
            Assert.Equal(ParkingLayoutErrors.OwnerRequired, (await sender.Send(new GetParkingLayoutQuery(lotId, registrantId), ct)).Error);
            Assert.False((await sender.Send(new ConfigureOpeningHoursCommand(lotId, registrantId, new WeeklyOpeningHours(
                Enumerable.Range(0, 7).Select(day => new DailyOpeningHours(day, true, false, null, null)).ToArray())), ct)).IsSuccess);
        }
    }

    [Fact]
    public async Task The_backfill_migration_gives_existing_registrants_an_owner_row_without_touching_other_rows()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        var admin = Guid.NewGuid();
        var registrantNoRow = Guid.NewGuid();
        var registrantWithOwnerRow = Guid.NewGuid();
        var registrantNowStaff = Guid.NewGuid();
        var otherOwner = Guid.NewGuid();
        var lotNoRow = Guid.NewGuid();
        var lotWithOwnerRow = Guid.NewGuid();
        var lotNowStaff = Guid.NewGuid();
        var lotCoOwned = Guid.NewGuid();
        var lotLegacy = Guid.NewGuid();
        var fixedTime = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var migrations = db.Database.GetMigrations().ToList();
        var backfill = Assert.Single(migrations, name => name.EndsWith(BackfillMigrationSuffix, StringComparison.Ordinal));
        var previous = migrations[migrations.IndexOf(backfill) - 1];
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(previous, ct);
        await InsertLotAsync(db, lotNoRow, registrantNoRow, "Backfill Lot 1", ct);
        await InsertLotAsync(db, lotWithOwnerRow, registrantWithOwnerRow, "Backfill Lot 2", ct);
        await InsertLotAsync(db, lotNowStaff, registrantNowStaff, "Backfill Lot 3", ct);
        await InsertLotAsync(db, lotCoOwned, registrantNoRow, "Backfill Lot 4", ct);
        await InsertLotAsync(db, lotLegacy, null, "Backfill Lot 5", ct);
        await InsertAssignmentAsync(db, lotWithOwnerRow, registrantWithOwnerRow, "Owner", admin, fixedTime, ct);
        await InsertAssignmentAsync(db, lotNowStaff, registrantNowStaff, "Staff", admin, fixedTime, ct);
        await InsertAssignmentAsync(db, lotCoOwned, otherOwner, "Owner", admin, fixedTime, ct);

        await migrator.MigrateAsync(null, ct);

        var rows = await db.ParkingLotAssignments.AsNoTracking().ToListAsync(ct);
        ParkingLotAssignment Row(Guid lot, Guid account) => Assert.Single(rows, r => r.LotId == lot && r.AccountId == account);

        var created = Row(lotNoRow, registrantNoRow);
        Assert.Equal(ParkingLotMemberRole.Owner, created.Role);
        Assert.Equal(registrantNoRow, created.AssignedBy);

        var untouched = Row(lotWithOwnerRow, registrantWithOwnerRow);
        Assert.Equal(ParkingLotMemberRole.Owner, untouched.Role);
        Assert.Equal(admin, untouched.AssignedBy);
        Assert.Equal(fixedTime, untouched.AssignedAtUtc);

        var promoted = Row(lotNowStaff, registrantNowStaff);
        Assert.Equal(ParkingLotMemberRole.Owner, promoted.Role);

        Assert.Equal(ParkingLotMemberRole.Owner, Row(lotCoOwned, registrantNoRow).Role);
        var coOwner = Row(lotCoOwned, otherOwner);
        Assert.Equal(admin, coOwner.AssignedBy);
        Assert.Equal(fixedTime, coOwner.AssignedAtUtc);

        Assert.DoesNotContain(rows, r => r.LotId == lotLegacy);
        Assert.Equal(5, rows.Count(r => new[] { lotNoRow, lotWithOwnerRow, lotNowStaff, lotCoOwned, lotLegacy }.Contains(r.LotId)));
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

    private static async Task<Guid> RegisterAsync(ServiceProvider provider, Guid ownerId, string name, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var registered = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new RegisterParkingLotCommand(ownerId, name, "1 Demo Street", 10.77m, 106.70m, "Outdoor"), ct);
        return registered.Id;
    }

    private static async Task ExecuteAsync(ServiceProvider provider, string sql, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.ExecuteSqlRawAsync(sql, ct);
    }

    private static Task InsertLotAsync(ParkingDbContext db, Guid lotId, Guid? ownerUserId, string name, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO parking.parking_lots (id, owner_user_id, name, address, latitude, longitude, type, approval_status)
            VALUES ({lotId}, {ownerUserId}, {name}, '1 Demo Street', 10.77, 106.70, 'Outdoor', 'Pending')
            """,
            ct);

    private static Task InsertAssignmentAsync(
        ParkingDbContext db, Guid lotId, Guid accountId, string role, Guid assignedBy, DateTimeOffset at, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO parking.parking_lot_assignments (lot_id, account_id, role, assigned_by, assigned_at_utc)
            VALUES ({lotId}, {accountId}, {role}, {assignedBy}, {at})
            """,
            ct);
}
