using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ParkingSystem.Booking.Application.Common.Options;
using ParkingSystem.Booking.WebAPI.Configurations;
using Xunit;

namespace ParkingSystem.Tests.Booking.IntegrationTests.Configuration;

public sealed class ReservationOptionsRegistrationTests
{
    [Fact]
    public void AddReservationOptions_binds_valid_configuration_and_resolves_successfully()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reservation:DepositAmountVnd"] = "25000",
                ["Reservation:HoldWhileDrivingDepositVnd"] = "30000"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddReservationOptions(configuration);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ReservationOptions>>().Value;

        Assert.Equal(25000m, options.DepositAmountVnd);
        Assert.Equal(30000m, options.HoldWhileDrivingDepositVnd);
        Assert.Equal(30000m, options.GetHoldWhileDrivingDeposit());
    }

    [Fact]
    public void AddReservationOptions_when_section_is_missing_uses_default_values_and_resolves_successfully()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddReservationOptions(configuration);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ReservationOptions>>().Value;

        Assert.Equal(20000m, options.DepositAmountVnd);
        Assert.Null(options.HoldWhileDrivingDepositVnd);
        Assert.Equal(20000m, options.GetHoldWhileDrivingDeposit());
    }

    [Theory]
    [InlineData("-1", "Reservation:DepositAmountVnd must not be negative.")]
    [InlineData("-50000", "Reservation:DepositAmountVnd must not be negative.")]
    public void AddReservationOptions_when_DepositAmountVnd_is_negative_fails_validation_on_start(
        string value,
        string expectedMessage)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reservation:DepositAmountVnd"] = value
            })
            .Build();

        var services = new ServiceCollection();
        services.AddReservationOptions(configuration);

        var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ReservationOptions>>().Value);

        Assert.Contains(expectedMessage, ex.Message);
    }

    [Theory]
    [InlineData("20000.5")]
    [InlineData("100.99")]
    public void AddReservationOptions_when_DepositAmountVnd_is_not_integer_fails_validation_on_start(string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reservation:DepositAmountVnd"] = value
            })
            .Build();

        var services = new ServiceCollection();
        services.AddReservationOptions(configuration);

        var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ReservationOptions>>().Value);

        Assert.Contains("Reservation:DepositAmountVnd must be an integer VND value.", ex.Message);
    }

    [Fact]
    public void AddReservationOptions_when_HoldWhileDrivingDepositVnd_is_negative_fails_validation_on_start()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reservation:DepositAmountVnd"] = "20000",
                ["Reservation:HoldWhileDrivingDepositVnd"] = "-1000"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddReservationOptions(configuration);

        var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ReservationOptions>>().Value);

        Assert.Contains("Reservation:HoldWhileDrivingDepositVnd must not be negative.", ex.Message);
    }

    [Fact]
    public void AddReservationOptions_when_HoldWhileDrivingDepositVnd_is_not_integer_fails_validation_on_start()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reservation:DepositAmountVnd"] = "20000",
                ["Reservation:HoldWhileDrivingDepositVnd"] = "15000.25"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddReservationOptions(configuration);

        var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<ReservationOptions>>().Value);

        Assert.Contains("Reservation:HoldWhileDrivingDepositVnd must be an integer VND value.", ex.Message);
    }

    [Fact]
    public void AddReservationOptions_throws_ArgumentNullException_for_null_arguments()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<ArgumentNullException>(() =>
            ReservationOptionsRegistration.AddReservationOptions(null!, configuration));

        Assert.Throws<ArgumentNullException>(() =>
            ReservationOptionsRegistration.AddReservationOptions(services, null!));
    }
}
