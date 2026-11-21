using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLots.AdminLots;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class AdminParkingAnalyticsIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Admin_listing_filters_inclusively_before_paging_and_returns_submission_timestamp()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await MigratedServicesAsync(ct);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var approved = Guid.NewGuid();
        var outside = Guid.NewGuid();
        await InsertLotAsync(provider, first, "A boundary", "Pending", Utc(2026, 10, 31, 17, 0, 0), ct);
        await InsertLotAsync(provider, second, "B boundary", "Pending", Utc(2026, 11, 20, 16, 59, 59), ct);
        await InsertLotAsync(provider, approved, "C approved", "Approved", Utc(2026, 11, 10, 5, 0, 0), ct);
        await InsertLotAsync(provider, outside, "D outside", "Pending", Utc(2026, 10, 31, 16, 59, 59), ct);

        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAdminParkingLotReader>();
        var result = await reader.ListForAdminAsync(
            ParkingLotApprovalStatus.Pending,
            Utc(2026, 10, 31, 17, 0, 0),
            Utc(2026, 11, 20, 17, 0, 0),
            page: 2,
            pageSize: 1,
            ct);

        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(second, result.Items[0].Id);
        Assert.Equal(Utc(2026, 11, 20, 16, 59, 59).UtcDateTime, result.Items[0].SubmittedAtUtc);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Items[0],
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Contains("\"submittedAtUtc\":\"2026-11-20T16:59:59Z\"", json);
    }

    [Fact]
    public async Task Trends_group_submissions_and_approval_audit_events_by_vietnam_day()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await MigratedServicesAsync(ct);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var rejected = Guid.NewGuid();
        await InsertLotAsync(provider, first, "Submitted 20", "Pending", Utc(2026, 11, 20, 16, 30, 0), ct);
        await InsertLotAsync(provider, second, "Submitted 21", "Pending", Utc(2026, 11, 20, 17, 10, 0), ct);
        await InsertLotAsync(provider, rejected, "Rejected", "Rejected", Utc(2026, 11, 20, 9, 0, 0), ct);
        await InsertAuditAsync(provider, first, AuditActions.ParkingLotApproved, Utc(2026, 11, 20, 17, 5, 0), ct);
        await InsertAuditAsync(provider, second, AuditActions.ParkingLotApproved, Utc(2026, 11, 21, 1, 0, 0), ct);
        await InsertAuditAsync(provider, rejected, AuditActions.ParkingLotRejected, Utc(2026, 11, 21, 2, 0, 0), ct);

        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAdminParkingAnalyticsReader>();
        var counts = await reader.GetCountsAsync(
            Utc(2026, 11, 19, 17, 0, 0).UtcDateTime,
            Utc(2026, 11, 21, 17, 0, 0).UtcDateTime,
            ct);

        Assert.Equal(new[] { new DailyParkingLotCount(new DateOnly(2026, 11, 20), 2), new DailyParkingLotCount(new DateOnly(2026, 11, 21), 1) }, counts.NewSubmissions);
        Assert.Equal(new[] { new DailyParkingLotCount(new DateOnly(2026, 11, 21), 2) }, counts.Approvals);
    }

    [Fact]
    public async Task Admin_listing_accepts_each_date_filter_independently_and_without_date_filters()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await MigratedServicesAsync(ct);
        var before = Guid.NewGuid();
        var middle = Guid.NewGuid();
        var after = Guid.NewGuid();
        await InsertLotAsync(provider, before, "Date before", "Approved", Utc(2001, 1, 1, 12, 0, 0), ct);
        await InsertLotAsync(provider, middle, "Date middle", "Approved", Utc(2001, 1, 2, 12, 0, 0), ct);
        await InsertLotAsync(provider, after, "Date after", "Approved", Utc(2001, 1, 3, 12, 0, 0), ct);

        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAdminParkingLotReader>();
        var noDates = await reader.ListForAdminAsync(ParkingLotApprovalStatus.Approved, null, null, 1, 100, ct);
        var fromOnly = await reader.ListForAdminAsync(ParkingLotApprovalStatus.Approved,
            Utc(2001, 1, 2, 12, 0, 0), null, 1, 100, ct);
        var toOnly = await reader.ListForAdminAsync(ParkingLotApprovalStatus.Approved,
            null, Utc(2001, 1, 3, 17, 0, 0), 1, 100, ct);

        Assert.True(noDates.TotalCount >= 3);
        Assert.Contains(noDates.Items, item => item.Id == before);
        Assert.Contains(noDates.Items, item => item.Id == middle);
        Assert.Contains(noDates.Items, item => item.Id == after);
        Assert.Equal(2, fromOnly.TotalCount);
        Assert.Contains(fromOnly.Items, item => item.Id == middle);
        Assert.Contains(fromOnly.Items, item => item.Id == after);
        Assert.Equal(3, toOnly.TotalCount);
        Assert.Contains(toOnly.Items, item => item.Id == before);
        Assert.Contains(toOnly.Items, item => item.Id == middle);
    }

    [Fact]
    public async Task Existing_lots_receive_unknown_date_sentinel_not_migration_time()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await MigratedServicesAsync(ct, "20261115115413_AddParkingSessionExitArrivalAndExtension");
        var id = Guid.NewGuid();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
                VALUES ({id}, 'Legacy lot', 'Legacy address', 10.700000, 106.700000, 'Outdoor', 'Pending')
                """, ct);
            await db.GetService<IMigrator>().MigrateAsync(cancellationToken: ct);
        }

        await using var verifyScope = provider.CreateAsyncScope();
        var persisted = await verifyScope.ServiceProvider.GetRequiredService<ParkingDbContext>().ParkingLots
            .AsNoTracking().SingleAsync(lot => lot.Id == id, ct);
        Assert.Equal(DateTimeKind.Utc, persisted.CreatedAt.Kind);
        Assert.Equal(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), persisted.CreatedAt);
    }

    private async Task<ServiceProvider> MigratedServicesAsync(CancellationToken cancellationToken, string? target = null)
    {
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        if (target is null)
            await db.Database.MigrateAsync(cancellationToken);
        else
            await db.GetService<IMigrator>().MigrateAsync(target, cancellationToken);
        return provider;
    }

    private static async Task InsertLotAsync(
        ServiceProvider provider, Guid id, string name, string status, DateTimeOffset createdAt, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status, created_at)
            VALUES ({id}, {name}, 'Test address', 10.700000, 106.700000, 'Outdoor', {status}, {createdAt})
            """, ct);
    }

    private static async Task InsertAuditAsync(
        ServiceProvider provider, Guid lotId, string action, DateTimeOffset occurredAt, CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.audit_logs (id, lot_id, actor_user_id, action, target_type, target_id, occurred_at)
            VALUES ({Guid.NewGuid()}, {lotId}, {AdminId}, {action}, 'ParkingLot', {lotId.ToString()}, {occurredAt})
            """, ct);
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute, int second) =>
        new(year, month, day, hour, minute, second, TimeSpan.Zero);
}
