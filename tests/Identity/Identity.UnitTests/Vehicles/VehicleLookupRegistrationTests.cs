using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Identity.Application.Vehicles.Lookup;
using ParkingSystem.Identity.Infrastructure.VehicleLookup;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Vehicles;

public sealed class VehicleLookupRegistrationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("Stub")]
    [InlineData("stub")]
    public void Stub_provider_is_registered_by_default_or_by_name(string? provider)
    {
        var services = new ServiceCollection();

        services.AddVehicleLookupReader(BuildConfiguration(provider));

        using var serviceProvider = services.BuildServiceProvider();
        Assert.IsType<StubVehicleLookupReader>(serviceProvider.GetRequiredService<IVehicleLookupReader>());
    }

    [Fact]
    public void Unknown_provider_fails_fast_at_startup()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddVehicleLookupReader(BuildConfiguration("Postgres")));

        Assert.Contains(VehicleLookupRegistration.ProviderKey, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Host_startup_rejects_a_missing_provider(string? provider)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            VehicleLookupRegistration.EnsureProviderConfigured(BuildConfiguration(provider)));

        Assert.Contains(VehicleLookupRegistration.ProviderKey, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_startup_accepts_an_explicit_provider()
    {
        VehicleLookupRegistration.EnsureProviderConfigured(BuildConfiguration("Stub"));
        Assert.True(VehicleLookupRegistration.IsStubConfigured(BuildConfiguration("stub")));
    }

    private static IConfiguration BuildConfiguration(string? provider) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [VehicleLookupRegistration.ProviderKey] = provider })
            .Build();
}
