using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MassTransit;
using ParkingSystem.Identity.Infrastructure;
using ParkingSystem.Identity.Infrastructure.Messaging.Outbox;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Identity.Persistence.Outbox;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

public sealed class OutboxDispatcherIntegrationTests(
    PostgreSqlContainerFixture postgres,
    RabbitMqContainerFixture rabbitMq)
    : IClassFixture<PostgreSqlContainerFixture>, IClassFixture<RabbitMqContainerFixture>
{
    [Fact]
    public async Task Dispatcher_publishes_successfully_retries_poison_rows_and_uses_expiring_leases()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configuration = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .AddConfiguration(RabbitMqTestConfiguration.Create(
                rabbitMq.Host, rabbitMq.AmqpPort, RabbitMqContainerFixture.Username, RabbitMqContainerFixture.Password, "/"))
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Outbox:PollingInterval"] = "00:00:00.050",
                ["Outbox:LeaseDuration"] = "00:00:05",
                ["Outbox:BaseRetryDelay"] = "00:00:02",
                ["Outbox:MaxRetryDelay"] = "00:00:04"
            })
            .Build();

        var eventType = "test.dispatcher.v1";
        var validId = Guid.NewGuid();
        var secondValidId = Guid.NewGuid();
        var malformedId = Guid.NewGuid();
        var unknownId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();

        var productionServices = new ServiceCollection();
        productionServices.AddLogging();
        productionServices.AddPersistence(configuration);
        productionServices.AddInfrastructure(configuration);
        productionServices.AddIntegrationEvent<TestIntegrationEvent>(eventType, 1);
        await using var productionProvider = productionServices.BuildServiceProvider(validateScopes: true);

        await using (var scope = productionProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
            await PostgreSqlOutboxAssertions.AssertServiceIsolationAndOutboxMetadataAsync(dbContext, "identity", cancellationToken, "accounts", "email_verifications", "refresh_tokens");
            dbContext.OutboxMessages.AddRange(
                Message(validId, eventType, """{"Value":"first"}""", correlationId, causationId),
                Message(secondValidId, eventType, """{"Value":"second"}""", null, null),
                Message(malformedId, eventType, """{"Value":42}""", null, null),
                Message(unknownId, "unknown.event.v1", "{}", null, null));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var capture = new ConsumedMessageCapture();
        var receiverServices = new ServiceCollection();
        receiverServices.AddLogging();
        receiverServices.AddSingleton(capture);
        receiverServices.AddMassTransit(bus =>
        {
            bus.AddConsumer<TestIntegrationEventConsumer>();
            bus.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(rabbitMq.Host, (ushort)rabbitMq.AmqpPort, "/", host =>
                {
                    host.Username(RabbitMqContainerFixture.Username);
                    host.Password(RabbitMqContainerFixture.Password);
                });
                rabbit.ReceiveEndpoint($"test-outbox-{Guid.NewGuid():N}", endpoint =>
                    endpoint.ConfigureConsumer<TestIntegrationEventConsumer>(context));
            });
        });

        await using var receiverProvider = receiverServices.BuildServiceProvider();
        var receiverBus = receiverProvider.GetRequiredService<IBusControl>();
        await receiverBus.StartAsync(cancellationToken);

        var productionBus = productionProvider.GetRequiredService<IBusControl>();
        var dispatcher = productionProvider.GetServices<IHostedService>().Single(service => service is OutboxDispatcher);
        await productionBus.StartAsync(cancellationToken);
        try
        {
            await dispatcher.StartAsync(cancellationToken);
            await WaitForDispatchStateAsync(
                productionProvider, [validId, secondValidId, malformedId, unknownId], cancellationToken);

            await using var verifyScope = productionProvider.CreateAsyncScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var rows = await verifyDb.OutboxMessages.AsNoTracking()
                .Where(message => new[] { validId, secondValidId, malformedId, unknownId }.Contains(message.Id))
                .ToDictionaryAsync(message => message.Id, cancellationToken);

            Assert.NotNull(rows[validId].ProcessedAtUtc);
            Assert.NotNull(rows[secondValidId].ProcessedAtUtc);
            Assert.Equal(0, rows[validId].RetryCount);
            Assert.Null(rows[validId].LockId);
            Assert.Null(rows[validId].LockedUntilUtc);
            Assert.Equal(1, rows[malformedId].RetryCount);
            Assert.Equal(1, rows[unknownId].RetryCount);
            Assert.Null(rows[malformedId].ProcessedAtUtc);
            Assert.Null(rows[unknownId].ProcessedAtUtc);
            Assert.False(string.IsNullOrWhiteSpace(rows[malformedId].Error));
            Assert.False(string.IsNullOrWhiteSpace(rows[unknownId].Error));
            Assert.True(rows[malformedId].NextAttemptAtUtc > DateTimeOffset.UtcNow);
            Assert.True(rows[unknownId].NextAttemptAtUtc > DateTimeOffset.UtcNow);
            Assert.Null(rows[malformedId].LockId);
            Assert.Null(rows[malformedId].LockedUntilUtc);
            Assert.Null(rows[unknownId].LockId);
            Assert.Null(rows[unknownId].LockedUntilUtc);

            await WaitForConsumedCountAsync(capture, 2, cancellationToken);
            var received = capture.Messages.ToArray();
            var receivedFirst = Assert.Single(received, message => message.MessageId == validId);
            Assert.Equal("first", receivedFirst.Value);
            Assert.Equal(correlationId, receivedFirst.CorrelationId);
            Assert.Equal(causationId, receivedFirst.InitiatorId);
            Assert.Equal(eventType, receivedFirst.EventType);
            Assert.Equal(1, receivedFirst.EventVersion);
            Assert.Contains(received, message => message.MessageId == secondValidId && message.Value == "second");

            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
            Assert.Equal(2, capture.Messages.Count);

            // The following assertions exercise store-level lease concurrency independently
            // from the background poller, which would otherwise race to claim the seeded row.
            await dispatcher.StopAsync(CancellationToken.None);

            // The two poison rows remain pending and retryable after the assertions above. Their
            // two-second retry backoff can legitimately expire while this combined integration
            // test performs the RabbitMQ and lease scenarios. This fixture uses SystemClock; no
            // fake clock is advanced. Defer only these test rows for the lease-only claim so its
            // eligible set is deterministic; do not mark them processed.
            var poisonIds = new[] { malformedId, unknownId };
            await using (var retryIsolationScope = productionProvider.CreateAsyncScope())
            {
                var db = retryIsolationScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                var poisonRows = await db.OutboxMessages.AsNoTracking()
                    .Where(message => poisonIds.Contains(message.Id))
                    .ToDictionaryAsync(message => message.Id, cancellationToken);

                foreach (var poisonId in poisonIds)
                {
                    var poisonRow = poisonRows[poisonId];
                    Assert.Null(poisonRow.ProcessedAtUtc);
                    Assert.Equal(1, poisonRow.RetryCount);
                    Assert.NotNull(poisonRow.NextAttemptAtUtc);
                    Assert.Null(poisonRow.LockId);
                    Assert.Null(poisonRow.LockedUntilUtc);
                }

                var leaseScenarioRetryAtUtc = DateTimeOffset.UtcNow.AddHours(1);
                await db.OutboxMessages
                    .Where(message => poisonIds.Contains(message.Id) && message.ProcessedAtUtc == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(message => message.NextAttemptAtUtc, (DateTimeOffset?)leaseScenarioRetryAtUtc),
                        cancellationToken);
            }

            var leaseId = Guid.NewGuid();
            await using (var seedScope = productionProvider.CreateAsyncScope())
            {
                var db = seedScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                db.OutboxMessages.Add(Message(leaseId, eventType, """{"Value":"lease"}""", null, null));
                await db.SaveChangesAsync(cancellationToken);
            }

            var lockOne = Guid.NewGuid();
            var lockTwo = Guid.NewGuid();
            var concurrentClaims = await Task.WhenAll(
                ClaimAsync(productionProvider, lockOne, cancellationToken),
                ClaimAsync(productionProvider, lockTwo, cancellationToken));
            var claimedLease = Assert.Single(concurrentClaims.SelectMany(batch => batch));
            Assert.Equal(leaseId, claimedLease.Id);
            Assert.Contains(claimedLease.LockId, new[] { lockOne, lockTwo });

            await using (var expireScope = productionProvider.CreateAsyncScope())
            {
                var db = expireScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                var row = await db.OutboxMessages.SingleAsync(message => message.Id == leaseId, cancellationToken);
                row.LockedUntilUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
                await db.SaveChangesAsync(cancellationToken);
            }

            var reclaimedLockId = Guid.NewGuid();
            var reclaimed = await ClaimAsync(productionProvider, reclaimedLockId, cancellationToken);
            Assert.Single(reclaimed);
            Assert.Equal(leaseId, reclaimed[0].Id);
            Assert.Equal(reclaimedLockId, reclaimed[0].LockId);

            await using var ownershipScope = productionProvider.CreateAsyncScope();
            var outboxStore = ownershipScope.ServiceProvider
                .GetRequiredService<ParkingSystem.Identity.Application.Common.Messaging.Outbox.IOutboxStore>();
            Assert.False(await outboxStore.MarkProcessedAsync(
                leaseId, claimedLease.LockId, DateTimeOffset.UtcNow, cancellationToken));
            Assert.True(await outboxStore.MarkProcessedAsync(
                leaseId, reclaimedLockId, DateTimeOffset.UtcNow, cancellationToken));
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
            await productionBus.StopAsync(CancellationToken.None);
            await receiverBus.StopAsync(CancellationToken.None);
        }
    }

    private static OutboxMessage Message(Guid id, string eventType, string payload, Guid? correlationId, Guid? causationId) =>
        new()
        {
            Id = id,
            EventType = eventType,
            EventVersion = 1,
            Payload = payload,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            RetryCount = 0,
            CorrelationId = correlationId,
            CausationId = causationId
        };

    private static async Task<IReadOnlyList<ParkingSystem.Identity.Application.Common.Messaging.Outbox.OutboxDispatchRecord>> ClaimAsync(
        ServiceProvider provider, Guid lockId, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ParkingSystem.Identity.Application.Common.Messaging.Outbox.IOutboxStore>()
            .ClaimPendingAsync(lockId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(10), 50, cancellationToken);
    }

    private static async Task WaitForDispatchStateAsync(
        ServiceProvider provider, Guid[] ids, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var rows = await db.OutboxMessages.AsNoTracking().Where(message => ids.Contains(message.Id)).ToArrayAsync(cancellationToken);
            if (rows.Length == ids.Length &&
                rows.Count(message => message.ProcessedAtUtc.HasValue) == 2 &&
                rows.Where(message => message.Id == ids[2] || message.Id == ids[3]).All(message => message.RetryCount == 1))
                return;
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        throw new TimeoutException("The dispatcher did not persist the expected success and retry states.");
    }

    private static async Task WaitForConsumedCountAsync(
        ConsumedMessageCapture capture, int count, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (capture.Messages.Count < count && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        Assert.Equal(count, capture.Messages.Count);
    }
}

public sealed record TestIntegrationEvent(string Value);

public sealed record ConsumedOutboxMessage(
    string Value,
    Guid? MessageId,
    Guid? CorrelationId,
    Guid? InitiatorId,
    string? EventType,
    int EventVersion);

public sealed class ConsumedMessageCapture
{
    public ConcurrentQueue<ConsumedOutboxMessage> Messages { get; } = new();
}

public sealed class TestIntegrationEventConsumer(ConsumedMessageCapture capture) : IConsumer<TestIntegrationEvent>
{
    public Task Consume(ConsumeContext<TestIntegrationEvent> context)
    {
        context.Headers.TryGetHeader("event-type", out var eventType);
        context.Headers.TryGetHeader("event-version", out var eventVersion);
        capture.Messages.Enqueue(new ConsumedOutboxMessage(
            context.Message.Value,
            context.MessageId,
            context.CorrelationId,
            context.InitiatorId,
            eventType?.ToString(),
            Convert.ToInt32(eventVersion, CultureInfo.InvariantCulture)));
        return Task.CompletedTask;
    }
}

