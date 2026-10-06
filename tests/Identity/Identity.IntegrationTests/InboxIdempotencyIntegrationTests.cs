using System.Collections.Concurrent;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Identity.Infrastructure;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Identity.Persistence.Inbox;
using ParkingSystem.Identity.Persistence.Outbox;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

public sealed class InboxIdempotencyIntegrationTests(
    PostgreSqlContainerFixture postgres,
    RabbitMqContainerFixture rabbitMq)
    : IClassFixture<PostgreSqlContainerFixture>, IClassFixture<RabbitMqContainerFixture>
{
    private const string ConsumerAKey = "identity.test-consumer-a.v1";
    private const string ConsumerBKey = "identity.test-consumer-b.v1";
    private const string ConsumerAEventType = "test.inbox-consumer-a.v1";
    private const string ConsumerBEventType = "test.inbox-consumer-b.v1";

    [Fact]
    public async Task Inbox_suppresses_duplicates_and_commits_or_rolls_back_with_the_handler_and_outbox()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configuration = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .AddConfiguration(RabbitMqTestConfiguration.Create(
                rabbitMq.Host,
                rabbitMq.AmqpPort,
                RabbitMqContainerFixture.Username,
                RabbitMqContainerFixture.Password,
                "/"))
            .Build();

        var capture = new InboxE2ECapture();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(configuration);
        services.AddIdempotentConsumer<InboxTestConsumerA>(ConsumerAKey);
        services.AddIdempotentConsumer<InboxTestConsumerB>(ConsumerBKey);
        services.AddSingleton(capture);
        services.AddInfrastructure(configuration, bus =>
        {
            bus.AddConsumer<InboxTestConsumerA>();
            bus.AddConsumer<InboxTestConsumerB>();
        });

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using (var scope = provider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
            await PostgreSqlOutboxAssertions.AssertServiceIsolationAndOutboxMetadataAsync(
                dbContext,
                "identity",
                cancellationToken,
                "accounts",
                "email_verifications",
                "refresh_tokens");
        }

        var bus = provider.GetRequiredService<IBusControl>();
        await bus.StartAsync(cancellationToken);
        try
        {
            var duplicateMessageId = Guid.NewGuid();
            var duplicateMessage = new InboxTestMessage("duplicate");
            await PublishAsync(bus, duplicateMessageId, duplicateMessage, cancellationToken);
            await WaitForRowsAsync(provider, duplicateMessageId, expectedInboxRows: 2, cancellationToken);

            var duplicateRows = await ReadInboxRowsAsync(provider, duplicateMessageId, cancellationToken);
            Assert.Equal(2, duplicateRows.Length);
            Assert.Contains(duplicateRows, row => row.ConsumerKey == ConsumerAKey && row.MessageId == duplicateMessageId);
            Assert.Contains(duplicateRows, row => row.ConsumerKey == ConsumerBKey && row.MessageId == duplicateMessageId);
            Assert.All(duplicateRows, row =>
            {
                Assert.NotEqual(default, row.ReceivedAtUtc);
                Assert.NotEqual(default, row.ProcessedAtUtc);
            });
            Assert.Equal(1, capture.DeliveryCount(duplicateMessageId, ConsumerAKey));
            Assert.Equal(1, capture.DeliveryCount(duplicateMessageId, ConsumerBKey));
            Assert.Single(capture.OutboxIds(duplicateMessageId, ConsumerAKey));
            Assert.Single(capture.OutboxIds(duplicateMessageId, ConsumerBKey));

            await PublishAsync(bus, duplicateMessageId, duplicateMessage, cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken);
            Assert.Equal(1, capture.DeliveryCount(duplicateMessageId, ConsumerAKey));
            Assert.Equal(1, capture.DeliveryCount(duplicateMessageId, ConsumerBKey));
            Assert.Equal(2, (await ReadInboxRowsAsync(provider, duplicateMessageId, cancellationToken)).Length);
            await AssertOutboxRowsExistAsync(provider, capture.OutboxIds(duplicateMessageId, ConsumerAKey), cancellationToken);
            await AssertOutboxRowsExistAsync(provider, capture.OutboxIds(duplicateMessageId, ConsumerBKey), cancellationToken);

            var concurrentMessageId = Guid.NewGuid();
            var concurrentMessage = new InboxTestMessage("concurrent", HoldConsumerA: true);
            await PublishAsync(bus, concurrentMessageId, concurrentMessage, cancellationToken);
            await capture.WaitForBlockedConsumerAAsync(concurrentMessageId, cancellationToken);
            await PublishAsync(bus, concurrentMessageId, concurrentMessage, cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            Assert.Equal(1, capture.DeliveryCount(concurrentMessageId, ConsumerAKey));
            capture.ReleaseConsumerA(concurrentMessageId);
            await WaitForRowsAsync(provider, concurrentMessageId, expectedInboxRows: 2, cancellationToken);
            Assert.Equal(1, capture.DeliveryCount(concurrentMessageId, ConsumerAKey));
            Assert.Equal(1, capture.DeliveryCount(concurrentMessageId, ConsumerBKey));
            Assert.Single(capture.OutboxIds(concurrentMessageId, ConsumerAKey));
            Assert.Single(capture.OutboxIds(concurrentMessageId, ConsumerBKey));
            await AssertOutboxRowsExistAsync(provider, capture.OutboxIds(concurrentMessageId, ConsumerAKey), cancellationToken);
            await AssertOutboxRowsExistAsync(provider, capture.OutboxIds(concurrentMessageId, ConsumerBKey), cancellationToken);

            var rollbackMessageId = Guid.NewGuid();
            var rollbackMessage = new InboxTestMessage("rollback", FailFirstConsumerAAttempt: true);
            await PublishAsync(bus, rollbackMessageId, rollbackMessage, cancellationToken);
            await capture.WaitForFailedConsumerAAsync(rollbackMessageId, cancellationToken);
            await WaitForRowsAsync(provider, rollbackMessageId, expectedInboxRows: 1, cancellationToken);

            var failedAttemptInboxRows = await ReadInboxRowsAsync(provider, rollbackMessageId, cancellationToken);
            Assert.Collection(failedAttemptInboxRows, row => Assert.Equal(ConsumerBKey, row.ConsumerKey));
            Assert.Equal(1, capture.DeliveryCount(rollbackMessageId, ConsumerAKey));
            Assert.Equal(1, capture.DeliveryCount(rollbackMessageId, ConsumerBKey));
            var failedAttemptOutboxId = Assert.Single(capture.OutboxIds(rollbackMessageId, ConsumerAKey));
            await AssertOutboxRowAbsentAsync(provider, failedAttemptOutboxId, cancellationToken);
            await AssertOutboxRowsExistAsync(provider, capture.OutboxIds(rollbackMessageId, ConsumerBKey), cancellationToken);

            await PublishAsync(bus, rollbackMessageId, rollbackMessage, cancellationToken);
            await WaitForRowsAsync(provider, rollbackMessageId, expectedInboxRows: 2, cancellationToken);
            var successfulRetryInboxRows = await ReadInboxRowsAsync(provider, rollbackMessageId, cancellationToken);
            Assert.Equal(2, successfulRetryInboxRows.Length);
            Assert.Contains(successfulRetryInboxRows, row => row.ConsumerKey == ConsumerAKey);
            Assert.Contains(successfulRetryInboxRows, row => row.ConsumerKey == ConsumerBKey);
            Assert.Equal(2, capture.DeliveryCount(rollbackMessageId, ConsumerAKey));
            Assert.Equal(1, capture.DeliveryCount(rollbackMessageId, ConsumerBKey));
            var consumerAOutboxIds = capture.OutboxIds(rollbackMessageId, ConsumerAKey);
            Assert.Equal(2, consumerAOutboxIds.Count);
            await AssertOutboxRowAbsentAsync(provider, failedAttemptOutboxId, cancellationToken);
            await AssertOutboxRowsExistAsync(provider, [consumerAOutboxIds[1]], cancellationToken);
            await AssertOutboxRowsExistAsync(provider, capture.OutboxIds(rollbackMessageId, ConsumerBKey), cancellationToken);
        }
        finally
        {
            capture.ReleaseAll();
            await bus.StopAsync(CancellationToken.None);
        }
    }

    private static Task PublishAsync(
        IBus bus,
        Guid messageId,
        InboxTestMessage message,
        CancellationToken cancellationToken) =>
        bus.Publish(message, context => context.MessageId = messageId, cancellationToken);

    private static async Task WaitForRowsAsync(
        ServiceProvider provider,
        Guid messageId,
        int expectedInboxRows,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var rows = await ReadInboxRowsAsync(provider, messageId, cancellationToken);
            if (rows.Length == expectedInboxRows && rows.All(row => row.ProcessedAtUtc != default))
                return;
            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        var finalRows = await ReadInboxRowsAsync(provider, messageId, cancellationToken);
        throw new TimeoutException($"Expected {expectedInboxRows} committed Inbox rows for message {messageId}, found {finalRows.Length}.");
    }

    private static async Task<InboxMessage[]> ReadInboxRowsAsync(
        ServiceProvider provider,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return await dbContext.InboxMessages.AsNoTracking()
            .Where(row => row.MessageId == messageId)
            .OrderBy(row => row.ConsumerKey)
            .ToArrayAsync(cancellationToken);
    }

    private static async Task AssertOutboxRowsExistAsync(
        ServiceProvider provider,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var found = await dbContext.OutboxMessages.AsNoTracking()
            .Where(row => ids.Contains(row.Id))
            .Select(row => row.Id)
            .ToArrayAsync(cancellationToken);
        Assert.Equal(ids.Count, found.Length);
    }

    private static async Task AssertOutboxRowAbsentAsync(
        ServiceProvider provider,
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.False(await dbContext.OutboxMessages.AsNoTracking().AnyAsync(row => row.Id == id, cancellationToken));
    }
}

public sealed record InboxTestMessage(
    string Value,
    bool FailFirstConsumerAAttempt = false,
    bool HoldConsumerA = false);

public sealed class InboxE2ECapture
{
    private readonly ConcurrentDictionary<(Guid MessageId, string ConsumerKey), int> _deliveries = new();
    private readonly ConcurrentDictionary<(Guid MessageId, string ConsumerKey), ConcurrentQueue<Guid>> _outboxIds = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _consumerABlocked = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _consumerARelease = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _consumerAFailed = new();

    public int RecordDelivery(Guid messageId, string consumerKey) =>
        _deliveries.AddOrUpdate((messageId, consumerKey), 1, static (_, count) => count + 1);

    public int DeliveryCount(Guid messageId, string consumerKey) =>
        _deliveries.TryGetValue((messageId, consumerKey), out var count) ? count : 0;

    public void RecordOutbox(Guid messageId, string consumerKey, Guid outboxId) =>
        _outboxIds.GetOrAdd((messageId, consumerKey), static _ => new ConcurrentQueue<Guid>()).Enqueue(outboxId);

    public IReadOnlyList<Guid> OutboxIds(Guid messageId, string consumerKey) =>
        _outboxIds.TryGetValue((messageId, consumerKey), out var ids) ? ids.ToArray() : [];

    public async Task BlockConsumerAAsync(Guid messageId, CancellationToken cancellationToken)
    {
        _consumerABlocked.GetOrAdd(messageId, static _ => NewSignal()).TrySetResult();
        await _consumerARelease.GetOrAdd(messageId, static _ => NewSignal()).Task.WaitAsync(cancellationToken);
    }

    public Task WaitForBlockedConsumerAAsync(Guid messageId, CancellationToken cancellationToken) =>
        _consumerABlocked.GetOrAdd(messageId, static _ => NewSignal()).Task.WaitAsync(cancellationToken);

    public void ReleaseConsumerA(Guid messageId) =>
        _consumerARelease.GetOrAdd(messageId, static _ => NewSignal()).TrySetResult();

    public void RecordConsumerAFailure(Guid messageId) =>
        _consumerAFailed.GetOrAdd(messageId, static _ => NewSignal()).TrySetResult();

    public Task WaitForFailedConsumerAAsync(Guid messageId, CancellationToken cancellationToken) =>
        _consumerAFailed.GetOrAdd(messageId, static _ => NewSignal()).Task.WaitAsync(cancellationToken);

    public void ReleaseAll()
    {
        foreach (var release in _consumerARelease.Values)
            release.TrySetResult();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed class InboxTestConsumerA(IdentityDbContext dbContext, InboxE2ECapture capture) : IConsumer<InboxTestMessage>
{
    public async Task Consume(ConsumeContext<InboxTestMessage> context)
    {
        var messageId = context.MessageId ?? throw new InvalidOperationException("The Inbox filter should require MessageId.");
        var attempt = capture.RecordDelivery(messageId, "identity.test-consumer-a.v1");
        var outboxId = Guid.NewGuid();
        dbContext.OutboxMessages.Add(CreateOutboxMessage(outboxId, "test.inbox-consumer-a.v1"));
        await dbContext.SaveChangesAsync(context.CancellationToken);
        capture.RecordOutbox(messageId, "identity.test-consumer-a.v1", outboxId);

        if (context.Message.FailFirstConsumerAAttempt && attempt == 1)
        {
            capture.RecordConsumerAFailure(messageId);
            throw new InvalidOperationException("Intentional test failure before Inbox transaction commit.");
        }

        if (context.Message.HoldConsumerA && attempt == 1)
            await capture.BlockConsumerAAsync(messageId, context.CancellationToken);
    }

    private static OutboxMessage CreateOutboxMessage(Guid id, string eventType) => new()
    {
        Id = id,
        EventType = eventType,
        EventVersion = 1,
        Payload = "{}",
        OccurredAtUtc = DateTimeOffset.UtcNow,
        RetryCount = 0
    };
}

public sealed class InboxTestConsumerB(IdentityDbContext dbContext, InboxE2ECapture capture) : IConsumer<InboxTestMessage>
{
    public async Task Consume(ConsumeContext<InboxTestMessage> context)
    {
        var messageId = context.MessageId ?? throw new InvalidOperationException("The Inbox filter should require MessageId.");
        capture.RecordDelivery(messageId, "identity.test-consumer-b.v1");
        var outboxId = Guid.NewGuid();
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = outboxId,
            EventType = "test.inbox-consumer-b.v1",
            EventVersion = 1,
            Payload = "{}",
            OccurredAtUtc = DateTimeOffset.UtcNow,
            RetryCount = 0
        });
        await dbContext.SaveChangesAsync(context.CancellationToken);
        capture.RecordOutbox(messageId, "identity.test-consumer-b.v1", outboxId);
    }
}
