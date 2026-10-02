using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ParkingSystem.Identity.Infrastructure;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Identity.IntegrationTests;

public sealed class MassTransitRabbitMqIntegrationTests(RabbitMqContainerFixture rabbitMq)
    : IClassFixture<RabbitMqContainerFixture>
{
    [Fact]
    public async Task Identity_bus_connects_and_stops_cleanly_against_disposable_rabbitmq()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configuration = RabbitMqTestConfiguration.Create(
            rabbitMq.Host,
            rabbitMq.AmqpPort,
            RabbitMqContainerFixture.Username,
            RabbitMqContainerFixture.Password,
            "/");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IBusControl>();
        await bus.StartAsync(cancellationToken);
        try
        {
            var report = await provider.GetRequiredService<HealthCheckService>()
                .CheckHealthAsync(check => check.Tags.Contains("ready"), cancellationToken);

            Assert.Equal(HealthStatus.Healthy, report.Status);
            Assert.Contains(report.Entries, entry => entry.Key.StartsWith("masstransit", StringComparison.Ordinal));
            Assert.Equal(HealthStatus.Healthy, report.Entries["rabbitmq-amqp"].Status);

        }
        finally
        {
            await bus.StopAsync(cancellationToken);
        }
    }
}
