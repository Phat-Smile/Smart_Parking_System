using System;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Features.DepositPolicy.Commands.ConfigureDepositPolicy;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public class ConfigureDepositPolicyCommandValidatorTests
{
    private readonly ConfigureDepositPolicyCommandValidator _validator = new();

    private static ConfigureDepositPolicyCommand CreateCommand(Guid? lotId = null, decimal holdDepositVnd = 20000m) =>
        new(lotId ?? Guid.NewGuid(), holdDepositVnd, Guid.NewGuid());

    [Fact]
    public void Validate_WhenLotIdIsEmpty_ShouldFailWithLotIdEmptyError()
    {
        var command = CreateCommand(lotId: Guid.Empty);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == ParkingErrors.DepositPolicy.LotIdEmpty.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-50000)]
    public void Validate_WhenHoldDepositVndIsNegative_ShouldFailWithDepositNegativeError(decimal deposit)
    {
        var command = CreateCommand(holdDepositVnd: deposit);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == ParkingErrors.DepositPolicy.DepositNegative.Message);
    }

    [Theory]
    [InlineData(100.5)]
    [InlineData(20000.25)]
    public void Validate_WhenHoldDepositVndIsNotInteger_ShouldFailWithDepositMustBeIntegerError(decimal deposit)
    {
        var command = CreateCommand(holdDepositVnd: deposit);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == ParkingErrors.DepositPolicy.DepositMustBeInteger.Message);
    }

    [Fact]
    public void Validate_WhenHoldDepositVndIsZero_ShouldPass()
    {
        var command = CreateCommand(holdDepositVnd: 0m);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(20000)]
    [InlineData(500000)]
    public void Validate_WhenHoldDepositVndIsValidInteger_ShouldPass(decimal deposit)
    {
        var command = CreateCommand(holdDepositVnd: deposit);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
