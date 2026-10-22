using System;
using Xunit;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Features.Pricing.Commands.ConfigurePricing;

namespace ParkingSystem.Tests.Parking.UnitTests;

public class ConfigurePricingCommandValidatorTests
{
    private readonly ConfigurePricingCommandValidator _validator = new();

    private ConfigurePricingCommand CreateValidCommand()
    {
        return new ConfigurePricingCommand
        {
            LotId = Guid.NewGuid(),
            VehicleType = "CAR",
            BillingMode = "Block",
            BlockMinutes = 30,
            Price = 10000m,
            OvernightFee = 50000m,
            ExpectedVersion = null,
            Reason = string.Empty,
            CallerId = Guid.NewGuid()
        };
    }

    // 1. Test LotId

    [Fact]
    public void Validate_WhenLotIdIsEmpty_ShouldFailWithLotIdEmptyError()
    {
        var command = CreateValidCommand();
        command.LotId = Guid.Empty;

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == ParkingErrors.Pricing.LotIdEmpty.Message);
    }

    // 2. Test loại xe (VehicleType)

    [Theory]
    [InlineData("CAR")]
    [InlineData("MOTORBIKE")]
    [InlineData("car")]
    [InlineData("motorbike")]
    [InlineData(" Car ")]
    public void Validate_WhenVehicleTypeIsValid_ShouldPass(string vehicleType)
    {
        var command = CreateValidCommand();
        command.VehicleType = vehicleType;

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("PLANE")]
    [InlineData("BOAT")]
    [InlineData("TRUCK")]
    public void Validate_WhenVehicleTypeIsInvalid_ShouldFailWithVehicleTypeInvalidError(string vehicleType)
    {
        var command = CreateValidCommand();
        command.VehicleType = vehicleType;

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == ParkingErrors.Pricing.VehicleTypeInvalid.Message);
    }

    // 3. Test chế độ tính phí (BillingMode)

    [Theory]
    [InlineData("PerEntry")]
    [InlineData("Block")]
    public void Validate_WhenBillingModeIsValid_ShouldPass(string billingMode)
    {
        var command = CreateValidCommand();
        command.BillingMode = billingMode;
        if (billingMode == "PerEntry")
        {
            command.BlockMinutes = null;
        }

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Hourly")]
    [InlineData("PerTurn")]
    [InlineData("Daily")]
    public void Validate_WhenBillingModeIsInvalid_ShouldFailWithBillingModeInvalidError(string billingMode)
    {
        var command = CreateValidCommand();
        command.BillingMode = billingMode;

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == ParkingErrors.Pricing.BillingModeInvalid.Message);
    }

    // 4. Test thời gian Block (BlockMinutes)

    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    public void Validate_WhenBillingModeIsBlock_AndBlockMinutesIsValid_ShouldPass(int minutes)
    {
        var command = CreateValidCommand();
        command.BillingMode = "Block";
        command.BlockMinutes = minutes;

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-15)]
    [InlineData(45)]
    public void Validate_WhenBillingModeIsBlock_AndBlockMinutesIsInvalid_ShouldFail(int? minutes)
    {
        var command = CreateValidCommand();
        command.BillingMode = "Block";
        command.BlockMinutes = minutes;

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == ParkingErrors.Pricing.BlockMinutesInvalid.Message);
    }

    [Fact]
    public void Validate_WhenBillingModeIsPerEntry_AndBlockMinutesIsNotNull_ShouldFail()
    {
        var command = CreateValidCommand();
        command.BillingMode = "PerEntry";
        command.BlockMinutes = 30;

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == ParkingErrors.Pricing.BlockMinutesInvalid.Message);
    }

    // 5. Test giá tiền và phí qua đêm

    [Theory]
    [InlineData(0)]
    [InlineData(-5000)]
    public void Validate_WhenPriceIsZeroOrNegative_ShouldFailWithPriceNegativeError(decimal price)
    {
        var command = CreateValidCommand();
        command.Price = price;

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == ParkingErrors.Pricing.PriceNegative.Message);
    }

    [Fact]
    public void Validate_WhenPriceHasFractionalVnd_ShouldFailWithPriceMustBeIntegerError()
    {
        var command = CreateValidCommand();
        command.Price = 10000.5m;

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == ParkingErrors.Pricing.PriceMustBeInteger.Message);
    }

    [Fact]
    public void Validate_WhenOvernightFeeIsNegative_ShouldFailWithOvernightFeeNegativeError()
    {
        var command = CreateValidCommand();
        command.OvernightFee = -1000m;

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == ParkingErrors.Pricing.OvernightFeeNegative.Message);
    }

    // 6. Test lý do khi cập nhật phiên bản giá

    [Fact]
    public void Validate_WhenExpectedVersionHasValue_AndReasonIsEmpty_ShouldFailWithReasonRequiredError()
    {
        var command = CreateValidCommand();
        command.ExpectedVersion = 1;
        command.Reason = string.Empty;

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == ParkingErrors.Pricing.ReasonRequired.Message);
    }

    [Fact]
    public void Validate_WhenExpectedVersionHasValue_AndReasonIsProvided_ShouldPass()
    {
        var command = CreateValidCommand();
        command.ExpectedVersion = 1;
        command.Reason = "Cập nhật bảng giá ngày lễ";

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
