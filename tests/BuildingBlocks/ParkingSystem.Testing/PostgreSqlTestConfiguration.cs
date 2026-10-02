using Microsoft.Extensions.Configuration;

namespace ParkingSystem.Testing;

public static class PostgreSqlTestConfiguration
{
    public static IConfiguration Create(string connectionString) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString
        })
        .Build();
}
