using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Persistence;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// The Availability provider switch: no database call is made, so no Docker is needed.
public sealed class AvailabilityRegistrationTests
{
    [Fact]
    public async Task Stub_provider_returns_the_configured_count()
    {
        await using var provider = BuildProvider(new() { ["Availability:Provider"] = "Stub", ["Availability:StubAvailableSlotCount"] = "0" });
        await using var scope = provider.CreateAsyncScope();

        var count = await scope.ServiceProvider.GetRequiredService<IAvailableSlotReader>()
            .CountAvailableCarSlotsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(0, count);
    }

    [Fact]
    public void Database_is_the_default_provider()
    {
        using var provider = BuildProvider([]);
        using var scope = provider.CreateScope();

        var reader = scope.ServiceProvider.GetRequiredService<IAvailableSlotReader>();

        Assert.Equal("AvailableSlotReader", reader.GetType().Name);
    }

    [Theory]
    [InlineData("Availability:Provider", "Random")]
    [InlineData("Availability:StubAvailableSlotCount", "-1")]
    [InlineData("Availability:StubAvailableSlotCount", "ten")]
    public void Invalid_configuration_fails_at_startup(string key, string value)
    {
        Assert.Throws<InvalidOperationException>(() => BuildProvider(new() { [key] = value }));
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        settings["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddPersistence(configuration).BuildServiceProvider();
    }
}
