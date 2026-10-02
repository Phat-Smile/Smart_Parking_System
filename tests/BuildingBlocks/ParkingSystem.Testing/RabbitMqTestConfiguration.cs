using Microsoft.Extensions.Configuration;

namespace ParkingSystem.Testing;

public static class RabbitMqTestConfiguration
{
    public static IConfiguration Create(string host, int port, string username, string password, string virtualHost)
    {
        var values = new Dictionary<string, string?>
        {
            ["RabbitMq:Host"] = host,
            ["RabbitMq:Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["RabbitMq:Username"] = username,
            ["RabbitMq:Password"] = password,
            ["RabbitMq:VirtualHost"] = virtualHost
        };

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
