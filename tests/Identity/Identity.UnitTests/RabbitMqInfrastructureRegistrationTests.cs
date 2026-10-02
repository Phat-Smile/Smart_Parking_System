using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ParkingSystem.Identity.Infrastructure;
using ParkingSystem.Identity.Infrastructure.Messaging;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class RabbitMqInfrastructureRegistrationTests
{
    [Fact]
    public void Missing_required_configuration_fails_options_validation()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
        {
            _ = provider.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        });

        Assert.Contains("RabbitMq:Host is required.", exception.Message);
        Assert.Contains("RabbitMq:Port must be between 1 and 65535.", exception.Message);
        Assert.Contains("RabbitMq:Username is required.", exception.Message);
        Assert.Contains("RabbitMq:Password is required.", exception.Message);
        Assert.Contains("RabbitMq:VirtualHost is required.", exception.Message);
    }

    [Fact]
    public async Task Valid_configuration_registers_a_bus_and_ready_health_check_without_connecting()
    {
        var values = new Dictionary<string, string?>
        {
            ["RabbitMq:Host"] = "broker-not-contacted-by-registration.test",
            ["RabbitMq:Port"] = "5672",
            ["RabbitMq:Username"] = "test-user",
            ["RabbitMq:Password"] = "test-password",
            ["RabbitMq:VirtualHost"] = "/"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddHealthChecks();
        services.AddInfrastructure(configuration);
        var provider = services.BuildServiceProvider();
        try
        {
            Assert.NotNull(provider.GetRequiredService<IBusControl>());
            var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
            Assert.Contains(registrations, registration =>
                registration.Tags.Contains("ready") && registration.Tags.Contains("masstransit"));
            Assert.Contains(registrations, registration =>
                registration.Name == "rabbitmq-amqp" && registration.Tags.Contains("ready"));
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }
}
