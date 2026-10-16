using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ParkingSystem.Payment.Application.Common.Messaging.Outbox;
using ParkingSystem.Payment.Application.Common.Messaging.Inbox;
using ParkingSystem.Payment.Infrastructure;
using ParkingSystem.Payment.Infrastructure.Messaging.Outbox;
using ParkingSystem.Payment.Persistence;
using ParkingSystem.Payment.Persistence.Outbox;
using ParkingSystem.Payment.Persistence.Inbox;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Payment.IntegrationTests;

public sealed class OutboxPersistenceIntegrationTests(PostgreSqlContainerFixture postgres, RabbitMqContainerFixture rabbitMq) : IClassFixture<PostgreSqlContainerFixture>, IClassFixture<RabbitMqContainerFixture>
{
    [Fact]
    public async Task Payment_migration_and_outbox_work_in_an_isolated_postgresql_database()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configuration = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .AddConfiguration(RabbitMqTestConfiguration.Create(rabbitMq.Host, rabbitMq.AmqpPort, RabbitMqContainerFixture.Username, RabbitMqContainerFixture.Password, "/"))
            .Build();
        var services = new ServiceCollection();
        services.AddPersistence(configuration);
        services.AddLogging();
        var inboxCapture = new InboxRegistrationProbeCapture();
        services.AddSingleton(inboxCapture);
        services.AddIdempotentConsumer<InboxRegistrationProbeConsumer>("test.payment.registration-probe.v1");
        services.AddInfrastructure(configuration, bus => bus.AddConsumer<InboxRegistrationProbeConsumer>());
        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        Assert.Contains(serviceProvider.GetServices<IHostedService>(), hostedService => hostedService is OutboxDispatcher);
        await using var scope = serviceProvider.CreateAsyncScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IOutboxStore>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IInboxExecutionCoordinator>());
        Assert.NotNull(serviceProvider.GetRequiredService<IConsumerDefinition<InboxRegistrationProbeConsumer>>());
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
        await PostgreSqlOutboxAssertions.AssertServiceIsolationAndOutboxMetadataAsync(dbContext, "payment", cancellationToken, "wallet_ledger_entries", "wallets");
        var probeMessageId = Guid.NewGuid();
        var inboxProbeBus = serviceProvider.GetRequiredService<IBusControl>();
        await inboxProbeBus.StartAsync(cancellationToken);
        try
        {
            await inboxProbeBus.Publish(
                new InboxRegistrationProbeMessage("Payment"),
                publish => publish.MessageId = probeMessageId,
                cancellationToken);

            var inboxDeadline = DateTimeOffset.UtcNow.AddSeconds(10);
            InboxMessage? receivedInboxMessage = null;
            while (DateTimeOffset.UtcNow < inboxDeadline)
            {
                receivedInboxMessage = await dbContext.InboxMessages.AsNoTracking()
                    .SingleOrDefaultAsync(message => message.MessageId == probeMessageId, cancellationToken);
                if (receivedInboxMessage is not null && inboxCapture.Count(probeMessageId) == 1)
                    break;
                await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
            }

            Assert.NotNull(receivedInboxMessage);
            Assert.Equal("test.payment.registration-probe.v1", receivedInboxMessage!.ConsumerKey);
            Assert.Equal(probeMessageId, receivedInboxMessage!.MessageId);
            Assert.NotEqual(default, receivedInboxMessage!.ReceivedAtUtc);
            Assert.NotEqual(default, receivedInboxMessage!.ProcessedAtUtc);
            Assert.Equal(1, inboxCapture.Count(probeMessageId));
        }
        finally
        {
            await inboxProbeBus.StopAsync(CancellationToken.None);
        }

        var id = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 10, 2, 9, 30, 0, TimeSpan.Zero);
        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = id,
            EventType = "integration-test",
            EventVersion = 1,
            Payload = """{"sample":true,"value":7}""",
            OccurredAtUtc = occurredAt,
            RetryCount = 0,
            CorrelationId = correlationId
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();

        var persisted = await dbContext.OutboxMessages.AsNoTracking().SingleAsync(message => message.Id == id, cancellationToken);
        Assert.Equal(id, persisted.Id);
        Assert.Equal("integration-test", persisted.EventType);
        Assert.Equal(1, persisted.EventVersion);
        Assert.Equal(occurredAt, persisted.OccurredAtUtc);
        Assert.Equal(0, persisted.RetryCount);
        Assert.Equal(correlationId, persisted.CorrelationId);
        Assert.Null(persisted.ProcessedAtUtc);
        using var payload = JsonDocument.Parse(persisted.Payload);
        Assert.True(payload.RootElement.GetProperty("sample").GetBoolean());
        Assert.Equal(7, payload.RootElement.GetProperty("value").GetInt32());
    }
}
