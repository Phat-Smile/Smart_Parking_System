using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

public sealed class OutboxWriterIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private sealed record WriterProbeEvent(Guid Id, string Name);

    [Fact]
    public async Task Enqueued_events_commit_and_roll_back_with_the_business_transaction()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);

        await using (var scope = serviceProvider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync(cancellationToken);

        // Rolled-back transaction: no Outbox row may survive.
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var writer = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            writer.Enqueue(new WriterProbeEvent(Guid.NewGuid(), "rolled-back"), "test.identity.writer-probe.v1", 1);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.RollbackAsync(cancellationToken);
        }

        await using (var scope = serviceProvider.CreateAsyncScope())
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().OutboxMessages.CountAsync(cancellationToken));

        // Committed change: the row carries the registered identity and metadata.
        var probe = new WriterProbeEvent(Guid.NewGuid(), "committed");
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();
        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            scope.ServiceProvider.GetRequiredService<IOutboxWriter>()
                .Enqueue(probe, "test.identity.writer-probe.v1", 1, correlationId, causationId);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().OutboxMessages
                .AsNoTracking()
                .SingleAsync(cancellationToken);
            Assert.Equal("test.identity.writer-probe.v1", row.EventType);
            Assert.Equal(1, row.EventVersion);
            Assert.Equal(correlationId, row.CorrelationId);
            Assert.Equal(causationId, row.CausationId);
            Assert.Null(row.ProcessedAtUtc);
            Assert.Equal(0, row.RetryCount);
            // The dispatcher deserializes with default options into the registered CLR type.
            Assert.Equal(probe, JsonSerializer.Deserialize<WriterProbeEvent>(row.Payload));
        }
    }

    [Fact]
    public void Writer_rejects_missing_event_identity()
    {
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        using var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        using var scope = serviceProvider.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();

        Assert.Throws<ArgumentException>(() => writer.Enqueue(new WriterProbeEvent(Guid.NewGuid(), "x"), " ", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.Enqueue(new WriterProbeEvent(Guid.NewGuid(), "x"), "test.identity.writer-probe.v1", 0));
    }
}
