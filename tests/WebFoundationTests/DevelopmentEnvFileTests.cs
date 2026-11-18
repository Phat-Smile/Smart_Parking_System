using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using ParkingSystem.AspNetCore;
using Xunit;

namespace ParkingSystem.Tests.WebFoundation;

public sealed class DevelopmentEnvFileTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"parking-env-test-{Guid.NewGuid():N}");

    public DevelopmentEnvFileTests()
    {
        Directory.CreateDirectory(root);
        File.WriteAllLines(Path.Combine(root, ".env"),
        [
            "# local compose values",
            "POSTGRES_DB=parking_system",
            "POSTGRES_USER=dev_user",
            "POSTGRES_PASSWORD=\"pg secret\"",
            "POSTGRES_HOST_PORT=5544",
            "RABBITMQ_USER=mq_user",
            "RABBITMQ_PASSWORD=mq_secret",
            "RABBITMQ_HOST_PORT=5673",
            "SECURITY_OTP_PEPPER=pepper-from-env-file",
            "SMTP_HOST=smtp.example.test"
        ]);
    }

    [Fact]
    public void Development_reads_credentials_from_the_env_file()
    {
        var configuration = Build("Development");

        Assert.Equal(
            "Host=127.0.0.1;Port=5544;Database=parking_system;Username=dev_user;Password=pg secret;GSS Encryption Mode=Disable",
            configuration["ConnectionStrings:DefaultConnection"]);
        Assert.Equal("127.0.0.1", configuration["RabbitMq:Host"]);
        Assert.Equal("5673", configuration["RabbitMq:Port"]);
        Assert.Equal("mq_user", configuration["RabbitMq:Username"]);
        Assert.Equal("mq_secret", configuration["RabbitMq:Password"]);
        Assert.Equal("/", configuration["RabbitMq:VirtualHost"]);
        Assert.Equal("pepper-from-env-file", configuration["Security:Otp:Pepper"]);
        Assert.Equal("smtp.example.test", configuration["SMTP_HOST"]);
    }

    [Fact]
    public void Shared_database_overrides_local_postgres_values()
    {
        File.AppendAllText(Path.Combine(root, ".env"), "\nDATABASE_CONNECTION_STRING=Host=shared.example.test;Database=postgres;SSL Mode=VerifyFull\n");
        var configuration = Build("Development");
        Assert.Equal("Host=shared.example.test;Database=postgres;SSL Mode=VerifyFull", configuration["ConnectionStrings:DefaultConnection"]);
    }

    [Fact]
    public void Existing_configuration_wins_over_shared_database_from_env_file()
    {
        File.AppendAllText(Path.Combine(root, ".env"), "\nDATABASE_CONNECTION_STRING=Host=shared.example.test;Database=postgres\n");
        var configuration = Build("Development", new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = "from-process" });
        Assert.Equal("from-process", configuration["ConnectionStrings:DefaultConnection"]);
    }

    [Fact]
    public void Values_already_configured_win_over_the_env_file()
    {
        var configuration = Build("Development", new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "from-compose",
            ["SMTP_HOST"] = "smtp.from-environment"
        });

        Assert.Equal("from-compose", configuration["ConnectionStrings:DefaultConnection"]);
        Assert.Equal("smtp.from-environment", configuration["SMTP_HOST"]);
        Assert.Equal("mq_secret", configuration["RabbitMq:Password"]);
    }

    [Fact]
    public void An_empty_placeholder_from_appsettings_is_filled_from_the_env_file()
    {
        var configuration = Build("Development", new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ""
        });

        Assert.StartsWith("Host=127.0.0.1;Port=5544;", configuration["ConnectionStrings:DefaultConnection"]);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Outside_development_the_env_file_is_ignored(string environment)
    {
        var configuration = Build(environment);

        Assert.Null(configuration["ConnectionStrings:DefaultConnection"]);
        Assert.Null(configuration["Security:Otp:Pepper"]);
        Assert.Null(configuration["SMTP_HOST"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private IConfiguration Build(string environment, Dictionary<string, string?>? existing = null)
    {
        // Empty builder: machine-level environment variables (ConnectionStrings__DefaultConnection, ...) must not leak in.
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { ContentRootPath = root, EnvironmentName = environment });
        if (existing is not null)
            builder.Configuration.AddInMemoryCollection(existing);
        builder.AddParkingSystemWebFoundation();
        return builder.Configuration;
    }
}
