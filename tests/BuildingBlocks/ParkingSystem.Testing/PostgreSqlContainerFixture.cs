using Testcontainers.PostgreSql;
using Xunit;

namespace ParkingSystem.Testing;


public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    private static readonly string? DirectConnectionString =
        Environment.GetEnvironmentVariable("TEST_POSTGRESQL_CONNECTION_STRING");

    private readonly PostgreSqlContainer? _container = DirectConnectionString is null
        ? new PostgreSqlBuilder("postgres:17.11-bookworm")
            .WithDatabase("parking_system_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCommand("-c", "max_connections=500")
            .Build()
        : null;

    public string ConnectionString => DirectConnectionString ?? _container!.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        if (_container is not null)
        {
            await _container.StartAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

