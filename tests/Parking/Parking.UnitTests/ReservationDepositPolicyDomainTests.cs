using System;
using ParkingSystem.Parking.Domain.Entities;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public class ReservationDepositPolicyDomainTests
{
    [Fact]
    public void Create_WhenLotIdIsEmpty_ShouldThrowArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ReservationDepositPolicy.Create(Guid.Empty, 20000m));

        Assert.Equal("lotId", ex.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-20000)]
    public void Create_WhenDepositIsNegative_ShouldThrowArgumentException(decimal negativeAmount)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ReservationDepositPolicy.Create(Guid.NewGuid(), negativeAmount));

        Assert.Contains("cannot be negative", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("holdDepositVnd", ex.ParamName);
    }

    [Theory]
    [InlineData(100.5)]
    [InlineData(20000.25)]
    public void Create_WhenDepositHasFractions_ShouldThrowArgumentException(decimal fractionalAmount)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ReservationDepositPolicy.Create(Guid.NewGuid(), fractionalAmount));

        Assert.Contains("must be an integer VND value", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("holdDepositVnd", ex.ParamName);
    }

    [Fact]
    public void Create_WhenDepositIsZero_ShouldSucceed()
    {
        var lotId = Guid.NewGuid();
        var policy = ReservationDepositPolicy.Create(lotId, 0m);

        Assert.NotNull(policy);
        Assert.NotEqual(Guid.Empty, policy.Id);
        Assert.Equal(lotId, policy.LotId);
        Assert.Equal(0m, policy.HoldDepositVnd);
    }

    [Fact]
    public void Create_WhenDepositIsValidPositiveInteger_ShouldSucceed()
    {
        var lotId = Guid.NewGuid();
        var policy = ReservationDepositPolicy.Create(lotId, 25000m);

        Assert.NotNull(policy);
        Assert.Equal(lotId, policy.LotId);
        Assert.Equal(25000m, policy.HoldDepositVnd);
    }

    [Theory]
    [InlineData(-100)]
    public void Update_WhenDepositIsNegative_ShouldThrowArgumentException(decimal negativeAmount)
    {
        var policy = ReservationDepositPolicy.Create(Guid.NewGuid(), 20000m);

        var ex = Assert.Throws<ArgumentException>(() => policy.Update(negativeAmount));

        Assert.Contains("cannot be negative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(15000.75)]
    public void Update_WhenDepositHasFractions_ShouldThrowArgumentException(decimal fractionalAmount)
    {
        var policy = ReservationDepositPolicy.Create(Guid.NewGuid(), 20000m);

        var ex = Assert.Throws<ArgumentException>(() => policy.Update(fractionalAmount));

        Assert.Contains("must be an integer VND value", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Update_WhenDepositIsZero_ShouldSucceed()
    {
        var policy = ReservationDepositPolicy.Create(Guid.NewGuid(), 20000m);

        policy.Update(0m);

        Assert.Equal(0m, policy.HoldDepositVnd);
    }

    [Fact]
    public void Update_WhenDepositIsValidPositiveInteger_ShouldSucceed()
    {
        var policy = ReservationDepositPolicy.Create(Guid.NewGuid(), 20000m);

        policy.Update(30000m);

        Assert.Equal(30000m, policy.HoldDepositVnd);
    }
}
