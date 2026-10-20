using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.OpeningHours;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class ParkingOpeningHoursIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Owner_can_save_read_and_filter_approved_lot_with_audit_and_idempotency()
    {
        var token = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(token);
        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(provider, "Approved", token);
        await AssignAsync(provider, lotId, ownerId, ParkingLotMemberRole.Owner, token);
        var schedule = MondayNineToFive();

        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var handler = new ConfigureOpeningHoursCommandHandler(
                services.GetRequiredService<IParkingLotOpeningHoursRepository>(),
                services.GetRequiredService<IAuditLog>(), services.GetRequiredService<IClock>());
            var result = await handler.Handle(new ConfigureOpeningHoursCommand(lotId, ownerId, schedule), token);
            Assert.True(result.IsSuccess);
            Assert.True((await handler.Handle(new ConfigureOpeningHoursCommand(lotId, ownerId, schedule), token)).IsSuccess);
        }

        await using var verifyScope = provider.CreateAsyncScope();
        var services2 = verifyScope.ServiceProvider;
        var reader = new GetOpeningHoursQueryHandler(services2.GetRequiredService<IParkingLotOpeningHoursRepository>());
        var publicView = await reader.Handle(new GetOpeningHoursQuery(lotId), token);
        Assert.True(publicView!.IsConfigured);
        Assert.Equal("09:00", publicView.Schedule!.Days[1].OpensAt);
        Assert.Equal(1, await services2.GetRequiredService<ParkingDbContext>().AuditLogEntries
            .CountAsync(entry => entry.LotId == lotId && entry.Action == "parking_lot.opening_hours_updated", token));

        var search = services2.GetRequiredService<IParkingLotArrivalSearch>();
        var open = await search.ListApprovedOpenAtAsync(new DateTimeOffset(2026, 10, 19, 3, 0, 0, TimeSpan.Zero), token);
        var closed = await search.ListApprovedOpenAtAsync(new DateTimeOffset(2026, 10, 19, 11, 0, 0, TimeSpan.Zero), token);
        Assert.Contains(open, lot => lot.Id == lotId);
        Assert.DoesNotContain(closed, lot => lot.Id == lotId);
    }

    [Fact]
    public async Task Staff_and_unassigned_owner_cannot_modify_a_lot()
    {
        var token = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(token);
        var lotId = await InsertLotAsync(provider, "Pending", token);
        var staffId = Guid.NewGuid();
        await AssignAsync(provider, lotId, staffId, ParkingLotMemberRole.Staff, token);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new ConfigureOpeningHoursCommandHandler(
            services.GetRequiredService<IParkingLotOpeningHoursRepository>(),
            services.GetRequiredService<IAuditLog>(), services.GetRequiredService<IClock>());
        Assert.False((await handler.Handle(new ConfigureOpeningHoursCommand(lotId, staffId, MondayNineToFive()), token)).IsSuccess);
        Assert.False((await handler.Handle(new ConfigureOpeningHoursCommand(lotId, Guid.NewGuid(), MondayNineToFive()), token)).IsSuccess);
        Assert.Null(await new GetOpeningHoursQueryHandler(services.GetRequiredService<IParkingLotOpeningHoursRepository>())
            .Handle(new GetOpeningHoursQuery(lotId), token));
        Assert.Null(await services.GetRequiredService<ParkingDbContext>().ParkingLots.AsNoTracking()
            .Where(lot => lot.Id == lotId).Select(lot => lot.OpeningHoursJson).SingleAsync(token));
    }

    [Fact]
    public async Task Owner_can_read_pending_lot_and_legacy_approved_lots_remain_visible()
    {
        var token = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(token);
        var ownerId = Guid.NewGuid();
        var pendingLotId = await InsertLotAsync(provider, "Pending", token);
        var approvedLotId = await InsertLotAsync(provider, "Approved", token);
        await AssignAsync(provider, pendingLotId, ownerId, ParkingLotMemberRole.Owner, token);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = new GetOpeningHoursQueryHandler(services.GetRequiredService<IParkingLotOpeningHoursRepository>());
        var pendingOwner = await reader.Handle(new GetOpeningHoursQuery(pendingLotId, ownerId), token);
        Assert.NotNull(pendingOwner);
        Assert.False(pendingOwner.IsConfigured);
        Assert.Null(await reader.Handle(new GetOpeningHoursQuery(pendingLotId), token));
        var visible = await services.GetRequiredService<IParkingLotArrivalSearch>()
            .ListApprovedOpenAtAsync(new DateTimeOffset(2026, 10, 19, 2, 0, 0, TimeSpan.Zero), token);
        Assert.Contains(visible, lot => lot.Id == approvedLotId);
        Assert.DoesNotContain(visible, lot => lot.Id == pendingLotId);
    }

    private async Task<ServiceProvider> NewProviderAsync(CancellationToken token)
    {
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(token);
        return provider;
    }

    private static async Task<Guid> InsertLotAsync(ServiceProvider provider, string approvalStatus, CancellationToken token)
    {
        var id = Guid.NewGuid();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
            VALUES ({id}, 'Owner Hours Test', '1 Demo Street', 10.77, 106.70, 'Outdoor', {approvalStatus})
            """,
            token);
        return id;
    }

    private static async Task AssignAsync(ServiceProvider provider, Guid lotId, Guid accountId,
        ParkingLotMemberRole role, CancellationToken token)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        db.ParkingLotAssignments.Add(ParkingLotAssignment.Create(lotId, accountId, role, Guid.NewGuid(), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(token);
    }

    private static WeeklyOpeningHours MondayNineToFive() => new(
        Enumerable.Range(0, 7).Select(day => day == 1
            ? new DailyOpeningHours(day, false, false, "09:00", "17:00")
            : new DailyOpeningHours(day, true, false, null, null)).ToArray());
}
