using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ParkingSystem.Parking.Application.Common.Identity;
using ParkingSystem.Parking.WebAPI.Grpc;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// Contract 1 wiring: no network call is made when the provider is built, so no Docker is needed.
public sealed class IdentityClientRegistrationTests
{
    private const string Endpoint = "http://identity-api:8081";

    [Fact]
    public async Task The_plate_lookup_port_is_served_by_the_identity_grpc_client()
    {
        await using var provider = BuildProvider(new() { ["IdentityService:GrpcEndpoint"] = Endpoint });
        await using var scope = provider.CreateAsyncScope();

        var lookup = scope.ServiceProvider.GetRequiredService<IPlateOwnerLookup>();

        Assert.IsType<IdentityVehicleGrpcClient>(lookup);
    }

    [Fact]
    public async Task The_deadline_defaults_to_two_seconds()
    {
        await using var provider = BuildProvider(new() { ["IdentityService:GrpcEndpoint"] = Endpoint });

        var options = provider.GetRequiredService<IOptions<IdentityServiceOptions>>().Value;

        Assert.Equal(Endpoint, options.GrpcEndpoint);
        Assert.Equal(2, options.TimeoutSeconds);
    }

    [Theory]
    [InlineData("IdentityService:GrpcEndpoint", "")]
    [InlineData("IdentityService:GrpcEndpoint", "identity-api:8081")]
    [InlineData("IdentityService:GrpcEndpoint", "/relative")]
    [InlineData("IdentityService:GrpcEndpoint", "ftp://identity-api:8081")]
    [InlineData("IdentityService:TimeoutSeconds", "0")]
    [InlineData("IdentityService:TimeoutSeconds", "-1")]
    [InlineData("IdentityService:TimeoutSeconds", "31")]
    public async Task Invalid_configuration_is_rejected(string key, string value)
    {
        var settings = new Dictionary<string, string?> { ["IdentityService:GrpcEndpoint"] = Endpoint, [key] = value };
        await using var provider = BuildProvider(settings);

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<IdentityServiceOptions>>().Value);
    }

    [Fact]
    public async Task A_missing_endpoint_is_rejected()
    {
        await using var provider = BuildProvider([]);

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<IdentityServiceOptions>>().Value);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection()
            .AddLogging()
            .AddIdentityServiceClient(configuration)
            .BuildServiceProvider();
    }
}
