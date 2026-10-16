using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.ReviewParkingLot;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class ParkingLotReviewIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Approving_pending_lot_is_persisted_and_audited_and_makes_lot_public()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await MigratedServicesAsync(ct);
        var lotId = await InsertPendingLotAsync(provider, ct);

        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = Handler(scope.ServiceProvider);
            var result = await handler.Handle(new(lotId, ParkingLotApprovalStatus.Approved, null, AdminId), ct);
            Assert.True(result.IsSuccess);
            Assert.Equal("Approved", result.Value!.ApprovalStatus);
            Assert.Null(result.Value.RejectionReason);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            var lot = await db.ParkingLots.AsNoTracking().SingleAsync(lot => lot.Id == lotId, ct);
            Assert.Equal(ParkingLotApprovalStatus.Approved, lot.ApprovalStatus);
            Assert.Null(lot.RejectionReason);

            var audit = await db.AuditLogEntries.AsNoTracking().SingleAsync(entry => entry.TargetId == lotId.ToString(), ct);
            Assert.Equal(AuditActions.ParkingLotApproved, audit.Action);
            Assert.Equal(AdminId, audit.ActorUserId);
            Assert.Equal("Pending", System.Text.Json.JsonDocument.Parse(audit.Details!).RootElement.GetProperty("fromStatus").GetString());
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = Handler(scope.ServiceProvider);
            var result = await handler.Handle(new(lotId, ParkingLotApprovalStatus.Approved, null, AdminId), ct);
            Assert.Equal(ReviewParkingLotErrors.AlreadyReviewed, result.Error);
        }
    }

    [Fact]
    public async Task Rejection_trims_and_persists_reason_and_does_not_make_lot_public()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await MigratedServicesAsync(ct);
        var lotId = await InsertPendingLotAsync(provider, ct);

        await using (var scope = provider.CreateAsyncScope())
        {
            var result = await Handler(scope.ServiceProvider).Handle(new(
                lotId, ParkingLotApprovalStatus.Rejected, " Missing fire documents ", AdminId), ct);
            Assert.True(result.IsSuccess);
            Assert.Equal("Rejected", result.Value!.ApprovalStatus);
            Assert.Equal("Missing fire documents", result.Value.RejectionReason);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            var lot = await db.ParkingLots.AsNoTracking().SingleAsync(lot => lot.Id == lotId, ct);
            Assert.Equal(ParkingLotApprovalStatus.Rejected, lot.ApprovalStatus);
            Assert.Equal("Missing fire documents", lot.RejectionReason);
            var audit = await db.AuditLogEntries.AsNoTracking().SingleAsync(entry => entry.TargetId == lotId.ToString(), ct);
            Assert.Equal(AuditActions.ParkingLotRejected, audit.Action);
            Assert.Equal("Missing fire documents", audit.Reason);
            var publicCount = await db.ParkingLots.AsNoTracking()
                .CountAsync(row => row.Id == lotId && row.ApprovalStatus == ParkingLotApprovalStatus.Approved, ct);
            Assert.Equal(0, publicCount);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var result = await Handler(scope.ServiceProvider).Handle(new(
                lotId, ParkingLotApprovalStatus.Approved, null, AdminId), ct);
            Assert.Equal(ReviewParkingLotErrors.AlreadyReviewed, result.Error);
        }
    }

    [Fact]
    public async Task Stale_second_decision_is_rejected_by_concurrency_token()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await MigratedServicesAsync(ct);
        var lotId = await InsertPendingLotAsync(provider, ct);
        await using var scopeOne = provider.CreateAsyncScope();
        await using var scopeTwo = provider.CreateAsyncScope();
        var first = scopeOne.ServiceProvider.GetRequiredService<IParkingLotApprovalRepository>();
        var second = scopeTwo.ServiceProvider.GetRequiredService<IParkingLotApprovalRepository>();

        var firstLot = await first.FindByIdAsync(lotId, ct);
        var secondLot = await second.FindByIdAsync(lotId, ct);
        Assert.NotNull(firstLot);
        Assert.NotNull(secondLot);
        Assert.True(firstLot.Approve());
        Assert.True(secondLot.Reject("Late conflicting decision"));
        Assert.True(await first.TrySaveChangesAsync(ct));
        Assert.False(await second.TrySaveChangesAsync(ct));

        await using var verificationScope = provider.CreateAsyncScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var persistedLot = await db.ParkingLots.AsNoTracking().SingleAsync(row => row.Id == lotId, ct);
        Assert.Equal(ParkingLotApprovalStatus.Approved, persistedLot.ApprovalStatus);
        Assert.Null(persistedLot.RejectionReason);
    }

    private async Task<ServiceProvider> MigratedServicesAsync(CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(cancellationToken);
        return provider;
    }

    private static async Task<Guid> InsertPendingLotAsync(ServiceProvider provider, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
            VALUES ({id}, 'Review Test Lot', '10 Test Street', 10.700000, 106.700000, 'Outdoor', 'Pending')
            """, cancellationToken);
        return id;
    }

    private static ReviewParkingLotCommandHandler Handler(IServiceProvider services) => new(
        services.GetRequiredService<IParkingLotApprovalRepository>(),
        services.GetRequiredService<IAuditLog>(),
        services.GetRequiredService<IClock>());
}
