using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.AssignLotMember;
using ParkingSystem.Parking.Application.ParkingLots.RemoveLotMember;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// TECH-12 trên PostgreSQL thật: gán, đổi vai trò, gỡ, và quyền theo bãi được tra lại từ bảng mỗi lần.
public sealed class ParkingLotAssignmentIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly Guid AdminId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    [Fact]
    public async Task Assigned_staff_can_operate_only_their_lot_until_removed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var lotA = await CreateLotAsync(provider, "Approved", cancellationToken);
        var lotB = await CreateLotAsync(provider, "Approved", cancellationToken);
        var staffId = Guid.NewGuid();

        Assert.True((await AssignAsync(provider, lotA, staffId, "staff", cancellationToken)).IsSuccess);

        Assert.True(await CanOperateAsync(provider, staffId, lotA, cancellationToken));
        Assert.False(await CanOperateAsync(provider, staffId, lotB, cancellationToken));

        Assert.True((await RemoveAsync(provider, lotA, staffId, cancellationToken)).IsSuccess);
        Assert.False(await CanOperateAsync(provider, staffId, lotA, cancellationToken));

        // Mỗi lần gán / gỡ đều có audit (NFR-16).
        await using var scope = provider.CreateAsyncScope();
        var actions = await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().AuditLogEntries.AsNoTracking()
            .Where(entry => entry.TargetId == staffId.ToString())
            .Select(entry => entry.Action)
            .ToListAsync(cancellationToken);
        Assert.Equal([AuditActions.LotMemberAssigned, AuditActions.LotMemberRemoved], actions.Order());
    }

    [Fact]
    public async Task Assigning_again_changes_the_role_in_place_and_the_same_role_is_a_no_op()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var lot = await CreateLotAsync(provider, "Pending", cancellationToken); // chủ bãi được gắn cả khi bãi chờ duyệt
        var accountId = Guid.NewGuid();

        await AssignAsync(provider, lot, accountId, "Staff", cancellationToken);
        var changed = await AssignAsync(provider, lot, accountId, "Owner", cancellationToken);
        var again = await AssignAsync(provider, lot, accountId, "owner", cancellationToken);

        Assert.Equal(nameof(ParkingLotMemberRole.Owner), changed.Value!.Role);
        // PostgreSQL lưu tới micro-giây nên so với sai số 1 ms.
        Assert.Equal(changed.Value.AssignedAtUtc, again.Value!.AssignedAtUtc, TimeSpan.FromMilliseconds(1));
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        Assert.Equal(1, await db.ParkingLotAssignments.CountAsync(a => a.LotId == lot && a.AccountId == accountId, cancellationToken));
        Assert.Equal(2, await db.AuditLogEntries.CountAsync(e => e.TargetId == accountId.ToString(), cancellationToken));
    }

    [Fact]
    public async Task Unknown_lot_and_unknown_member_are_reported_as_not_found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var lot = await CreateLotAsync(provider, "Approved", cancellationToken);

        Assert.Equal(LotMemberErrors.LotNotFound, (await AssignAsync(provider, Guid.NewGuid(), Guid.NewGuid(), "Staff", cancellationToken)).Error);
        Assert.Equal(LotMemberErrors.MemberNotFound, (await RemoveAsync(provider, lot, Guid.NewGuid(), cancellationToken)).Error);
    }

    [Fact]
    public async Task Foreign_key_rejects_an_assignment_for_a_lot_that_does_not_exist()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        await using var scope = provider.CreateAsyncScope();
        var assignments = scope.ServiceProvider.GetRequiredService<IParkingLotAssignmentRepository>();
        assignments.Add(ParkingLotAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), ParkingLotMemberRole.Staff, AdminId, DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => assignments.SaveChangesAsync(cancellationToken));
    }

    private async Task<ServiceProvider> CreateProviderAsync(CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        var provider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(cancellationToken);
        return provider;
    }

    private static async Task<Guid> CreateLotAsync(ServiceProvider provider, string approvalStatus, CancellationToken cancellationToken)
    {
        var lotId = Guid.NewGuid();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
            VALUES ({lotId}, 'Assignment Test Parking', '1 Demo Street', 10.776530, 106.700981, 'Outdoor', {approvalStatus})
            """,
            cancellationToken);
        return lotId;
    }

    private static async Task<Result<LotMember>> AssignAsync(ServiceProvider provider, Guid lotId, Guid accountId, string role, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new AssignLotMemberCommandHandler(
            services.GetRequiredService<IParkingLotAssignmentRepository>(),
            services.GetRequiredService<IAuditLog>(),
            services.GetRequiredService<IClock>());
        return await handler.Handle(new AssignLotMemberCommand(lotId, accountId, role, AdminId), cancellationToken);
    }

    private static async Task<Result<bool>> RemoveAsync(ServiceProvider provider, Guid lotId, Guid accountId, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var handler = new RemoveLotMemberCommandHandler(
            services.GetRequiredService<IParkingLotAssignmentRepository>(),
            services.GetRequiredService<IAuditLog>(),
            services.GetRequiredService<IClock>());
        return await handler.Handle(new RemoveLotMemberCommand(lotId, accountId, AdminId), cancellationToken);
    }

    private static async Task<bool> CanOperateAsync(ServiceProvider provider, Guid accountId, Guid lotId, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IParkingLotAccess>().CanOperateAsync(accountId, lotId, cancellationToken);
    }
}
