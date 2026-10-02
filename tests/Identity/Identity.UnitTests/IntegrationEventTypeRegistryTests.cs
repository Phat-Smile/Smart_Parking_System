using Microsoft.Extensions.DependencyInjection;
using MassTransit;
using ParkingSystem.Identity.Infrastructure.Messaging.Outbox;
using ParkingSystem.Identity.Infrastructure;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class IntegrationEventTypeRegistryTests
{
    [Fact]
    public void Explicit_event_mapping_resolves_by_logical_name_and_version()
    {
        var services = new ServiceCollection();
        services.AddIntegrationEvent<TestContract>("identity.test.v2", 2);
        using var provider = services.BuildServiceProvider();

        var resolver = provider.GetRequiredService<IIntegrationEventTypeResolver>();

        Assert.Equal(typeof(TestContract), resolver.Resolve("identity.test.v2", 2));
        Assert.Null(resolver.Resolve("identity.test.v1", 1));
    }

    [Fact]
    public void Duplicate_logical_name_and_version_fails_when_registry_is_resolved()
    {
        var services = new ServiceCollection();
        services.AddIntegrationEvent<TestContract>("identity.test.v1", 1);
        services.AddIntegrationEvent<TestContract>("identity.test.v1", 1);
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IIntegrationEventTypeResolver>());

        Assert.Contains("registered more than once", exception.Message);
    }

    [Fact]
    public void Duplicate_consumer_key_fails_during_registration()
    {
        var services = new ServiceCollection();
        services.AddIdempotentConsumer<FirstTestConsumer>("identity.test-handler.v1");

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddIdempotentConsumer<SecondTestConsumer>("identity.test-handler.v1"));

        Assert.Contains("already registered", exception.Message);
    }

    [Fact]
    public void Consumer_type_cannot_be_registered_twice()
    {
        var services = new ServiceCollection();
        services.AddIdempotentConsumer<FirstTestConsumer>("identity.test-handler.v1");

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddIdempotentConsumer<FirstTestConsumer>("identity.test-handler.v2"));

        Assert.Contains("already registered", exception.Message);
    }

    private sealed record TestContract(string Value);

    private sealed class FirstTestConsumer : IConsumer<TestContract>
    {
        public Task Consume(ConsumeContext<TestContract> context) => Task.CompletedTask;
    }

    private sealed class SecondTestConsumer : IConsumer<TestContract>
    {
        public Task Consume(ConsumeContext<TestContract> context) => Task.CompletedTask;
    }
}

