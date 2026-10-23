using ParkingSystem.Booking.Application.Common.Options;
using Xunit;

namespace ParkingSystem.Tests.Booking.UnitTests.Options;

public sealed class ReservationOptionsTests
{
    [Fact]
    public void Default_values_are_initialized_correctly()
    {
        var options = new ReservationOptions();

        Assert.Equal(20000m, options.DepositAmountVnd);
        Assert.Null(options.HoldWhileDrivingDepositVnd);
        Assert.Equal(20000m, options.GetHoldWhileDrivingDeposit());
        Assert.Equal("Reservation", ReservationOptions.SectionName);
    }

    [Fact]
    public void GetHoldWhileDrivingDeposit_returns_override_when_set()
    {
        var options = new ReservationOptions
        {
            DepositAmountVnd = 10000m,
            HoldWhileDrivingDepositVnd = 25000m
        };

        Assert.Equal(10000m, options.DepositAmountVnd);
        Assert.Equal(25000m, options.HoldWhileDrivingDepositVnd);
        Assert.Equal(25000m, options.GetHoldWhileDrivingDeposit());
    }

    [Fact]
    public void Custom_deposit_amount_without_override_is_returned()
    {
        var options = new ReservationOptions
        {
            DepositAmountVnd = 30000m
        };

        Assert.Equal(30000m, options.GetHoldWhileDrivingDeposit());
    }
}
