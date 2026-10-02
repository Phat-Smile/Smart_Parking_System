using Testcontainers.RabbitMq;
using Xunit;

namespace ParkingSystem.Testing;

public sealed class RabbitMqContainerFixture : IAsyncLifetime
{
    public const int AmqpContainerPort = 5672;
    public const string Username = "parking_test";
    public const string Password = "parking_test_local_only";

    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:4.3.6-management")
        .WithUsername(Username)
        .WithPassword(Password)
        .Build();

    public string Host => _container.Hostname;
    public int AmqpPort => _container.GetMappedPublicPort(AmqpContainerPort);

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
