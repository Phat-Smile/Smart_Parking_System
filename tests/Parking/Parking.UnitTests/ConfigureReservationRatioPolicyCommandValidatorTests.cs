using System;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Features.ReservationRatioPolicy.Commands.ConfigureReservationRatioPolicy;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public class ConfigureReservationRatioPolicyCommandValidatorTests
{
    private readonly ConfigureReservationRatioPolicyCommandValidator _validator = new();

    private static ConfigureReservationRatioPolicyCommand CreateCommand(Guid? lotId = null, int? ratioPercent = 50) =>
        new(lotId ?? Guid.NewGuid(), ratioPercent, Guid.NewGuid());

    [Fact]
    public void Validate_WhenReservationRatioPercentIsNull_ShouldFailWithRatioRequiredError()
    {
        var command = CreateCommand(ratioPercent: null);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == ParkingErrors.ReservationRatioPolicy.RatioRequired.Message);
    }

    [Fact]
    public void Validate_WhenLotIdIsEmpty_ShouldFailWithLotIdEmptyError()
    {
        var command = CreateCommand(lotId: Guid.Empty);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == ParkingErrors.ReservationRatioPolicy.LotIdEmpty.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-50)]
    public void Validate_WhenRatioIsNegative_ShouldFailWithRatioOutOfRangeError(int ratio)
    {
        var command = CreateCommand(ratioPercent: ratio);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == ParkingErrors.ReservationRatioPolicy.RatioOutOfRange.Message);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(200)]
    public void Validate_WhenRatioExceeds100_ShouldFailWithRatioOutOfRangeError(int ratio)
    {
        var command = CreateCommand(ratioPercent: ratio);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == ParkingErrors.ReservationRatioPolicy.RatioOutOfRange.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(100)]
    public void Validate_WhenRatioIsValid_ShouldPass(int ratio)
    {
        var command = CreateCommand(ratioPercent: ratio);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
