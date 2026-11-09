using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Notification.Application.CustomerNotifications;
using ParkingSystem.Notification.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Notification.IntegrationTests;

public sealed class CustomerNotificationStoreIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Inbox_is_deduplicated_and_read_is_scoped_to_customer()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await db.Database.MigrateAsync(ct);
        var store = scope.ServiceProvider.GetRequiredService<ICustomerNotificationStore>();
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var eventKey = $"test:{Guid.NewGuid()}";
        var now = DateTimeOffset.UtcNow;

        await store.AddIfAbsentAsync(owner, eventKey, "wallet", "Top-up", "Balance updated",
            "/customer/wallet", now, ct);
        await store.AddIfAbsentAsync(owner, eventKey, "wallet", "Top-up", "Balance updated",
            "/customer/wallet", now, ct);
        var page = await store.ListAsync(owner, false, 1, 20, ct);
        Assert.Single(page.Items);
        Assert.Equal(1, page.UnreadCount);
        Assert.False(await store.MarkReadAsync(stranger, page.Items[0].Id, now, ct));
        Assert.True(await store.MarkReadAsync(owner, page.Items[0].Id, now, ct));
        Assert.Equal(0, (await store.ListAsync(owner, false, 1, 20, ct)).UnreadCount);
        Assert.Empty((await store.ListAsync(stranger, false, 1, 20, ct)).Items);
    }
}
