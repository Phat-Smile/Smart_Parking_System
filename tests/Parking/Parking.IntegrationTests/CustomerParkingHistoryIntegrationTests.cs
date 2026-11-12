using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application.ParkingSessions.ListMyParkingSessionHistory;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class CustomerParkingHistoryIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task History_is_paginated_and_never_contains_another_users_sessions()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        await db.Database.MigrateAsync(ct);
        var lotId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
            VALUES ({lotId}, 'History test', 'Demo', 10.77, 106.70, 'Outdoor', 'Approved')
            """, ct);
        var mine = Guid.NewGuid();
        var other = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.ParkingSessions.Add(ParkingSession.Open(Guid.NewGuid(), lotId, "51A11111", VehicleType.Car,
            now, "qr-test-1", mine, null));
        db.ParkingSessions.Add(ParkingSession.Open(Guid.NewGuid(), lotId, "51A22222", VehicleType.Car,
            now.AddMinutes(1), "qr-test-2", mine, null));
        db.ParkingSessions.Add(ParkingSession.Open(Guid.NewGuid(), lotId, "51A33333", VehicleType.Car,
            now.AddMinutes(2), "qr-test-3", other, null));
        await db.SaveChangesAsync(ct);

        var reader = scope.ServiceProvider.GetRequiredService<IMyParkingSessionHistoryReader>();
        var page = await reader.ListAsync(mine, null, 0, 1, ct);
        var next = await reader.ListAsync(mine, null, 1, 1, ct);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal("51A22222", Assert.Single(page.Items).PlateNumber);
        Assert.Equal("51A11111", Assert.Single(next.Items).PlateNumber);
    }
}
