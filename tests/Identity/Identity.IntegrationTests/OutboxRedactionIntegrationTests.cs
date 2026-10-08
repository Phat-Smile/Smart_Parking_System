using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Identity.Persistence.Outbox;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

// The OTP travels in the Outbox payload; once published it must not stay readable in the table.
public sealed class OutboxRedactionIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Marking_processed_removes_redacted_keys_and_keeps_the_rest_of_the_payload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var id = await SeedAsync(provider, cancellationToken);

        await using (var scope = provider.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
            var now = DateTimeOffset.UtcNow;
            var claimed = Assert.Single(await store.ClaimPendingAsync(Guid.NewGuid(), now, now.AddSeconds(30), 10, cancellationToken), r => r.Id == id);
            Assert.True(await store.MarkProcessedAsync(claimed.Id, claimed.LockId, now, cancellationToken, ["OtpCode"]));
        }

        var payload = await ReadPayloadAsync(provider, id, cancellationToken);
        Assert.False(payload.TryGetProperty("OtpCode", out _));
        Assert.Equal("otp-redaction@example.com", payload.GetProperty("Email").GetString());
    }

    [Fact]
    public async Task Marking_processed_without_redacted_keys_keeps_the_payload_unchanged()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var id = await SeedAsync(provider, cancellationToken);

        await using (var scope = provider.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
            var now = DateTimeOffset.UtcNow;
            var claimed = Assert.Single(await store.ClaimPendingAsync(Guid.NewGuid(), now, now.AddSeconds(30), 10, cancellationToken), r => r.Id == id);
            Assert.True(await store.MarkProcessedAsync(claimed.Id, claimed.LockId, now, cancellationToken));
        }

        var payload = await ReadPayloadAsync(provider, id, cancellationToken);
        Assert.Equal("123456", payload.GetProperty("OtpCode").GetString());
    }

    private async Task<ServiceProvider> CreateProviderAsync(CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        // Each test claims its own row; clear leftovers so ClaimPendingAsync sees only this test's message.
        await db.OutboxMessages.ExecuteDeleteAsync(cancellationToken);
        return provider;
    }

    private static async Task<Guid> SeedAsync(ServiceProvider provider, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var id = Guid.NewGuid();
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = id,
            EventType = "identity.user-verification-otp-issued.v1",
            EventVersion = 1,
            Payload = """{"AccountId":"10000000-0000-0000-0000-000000000001","Email":"otp-redaction@example.com","OtpCode":"123456"}""",
            OccurredAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return id;
    }

    private static async Task<JsonElement> ReadPayloadAsync(ServiceProvider provider, Guid id, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var row = await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == id, cancellationToken);
        Assert.NotNull(row.ProcessedAtUtc);
        return JsonDocument.Parse(row.Payload).RootElement.Clone();
    }
}
