using System;
using ParkingSystem.Parking.Domain.Entities;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public class ReservationRatioPolicyDomainTests
{
    [Fact]
    public void DefaultReservationRatioPercent_ShouldBeFifty()
    {
        Assert.Equal(50, ReservationRatioPolicy.DefaultReservationRatioPercent);
    }

    [Fact]
    public void Create_WhenLotIdIsEmpty_ShouldThrowArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ReservationRatioPolicy.Create(Guid.Empty, 50));

        Assert.Equal("lotId", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(100)]
    public void Create_WhenRatioIsValid_ShouldSucceed(int ratioPercent)
    {
        var lotId = Guid.NewGuid();
        var policy = ReservationRatioPolicy.Create(lotId, ratioPercent);

        Assert.NotNull(policy);
        Assert.NotEqual(Guid.Empty, policy.Id);
        Assert.Equal(lotId, policy.LotId);
        Assert.Equal(ratioPercent, policy.ReservationRatioPercent);
        Assert.True(policy.UpdatedAtUtc <= DateTime.UtcNow);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-50)]
    [InlineData(101)]
    [InlineData(200)]
    public void Create_WhenRatioIsOutOfRange_ShouldThrowArgumentOutOfRangeException(int invalidRatio)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ReservationRatioPolicy.Create(Guid.NewGuid(), invalidRatio));

        Assert.Equal("ratioPercent", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(99)]
    [InlineData(100)]
    public void Update_WhenRatioIsValid_ShouldSucceed(int ratioPercent)
    {
        var policy = ReservationRatioPolicy.Create(Guid.NewGuid(), 50);
        policy.Update(ratioPercent);

        Assert.Equal(ratioPercent, policy.ReservationRatioPercent);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Update_WhenRatioIsOutOfRange_ShouldThrowArgumentOutOfRangeException(int invalidRatio)
    {
        var policy = ReservationRatioPolicy.Create(Guid.NewGuid(), 50);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            policy.Update(invalidRatio));

        Assert.Equal("ratioPercent", ex.ParamName);
    }

    [Theory]
    [InlineData(10, 50, 5)]
    [InlineData(9, 50, 4)]
    [InlineData(1, 50, 0)]
    [InlineData(0, 50, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(0, 100, 0)]
    [InlineData(50, 100, 50)]
    [InlineData(25, 0, 0)]
    [InlineData(7, 33, 2)]
    [InlineData(100, 1, 1)]
    public void CalculateReservedSlots_ShouldMatchExpectedCalculations(int capacity, int ratio, int expectedSlots)
    {
        var actual = ReservationRatioPolicy.CalculateReservedSlots(capacity, ratio);
        Assert.Equal(expectedSlots, actual);

        var policy = ReservationRatioPolicy.Create(Guid.NewGuid(), ratio);
        Assert.Equal(expectedSlots, policy.CalculateReservedSlots(capacity));
    }

    [Fact]
    public void CalculateReservedSlots_NeverExceedsEligibleCapacityAndNeverNegative()
    {
        int[] testCapacities = [0, 1, 5, 10, 23, 77, 100, 500, 1000];
        for (int ratio = 0; ratio <= 100; ratio++)
        {
            foreach (var capacity in testCapacities)
            {
                var slots = ReservationRatioPolicy.CalculateReservedSlots(capacity, ratio);
                Assert.InRange(slots, 0, capacity);
            }
        }
    }

    [Fact]
    public void CalculateReservedSlots_WhenCapacityIsNegative_ShouldThrowArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ReservationRatioPolicy.CalculateReservedSlots(-1, 50));

        Assert.Equal("eligibleCapacity", ex.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void CalculateReservedSlots_WhenRatioIsOutOfRange_ShouldThrowArgumentOutOfRangeException(int invalidRatio)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ReservationRatioPolicy.CalculateReservedSlots(10, invalidRatio));

        Assert.Equal("ratioPercent", ex.ParamName);
    }
}
