using System;
using ParkingSystem.Parking.Domain.Entities;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public class ReservationAvailabilityPolicyDomainTests
{
    [Fact]
    public void DefaultReservationEnabled_ShouldBeTrue()
    {
        Assert.True(ReservationAvailabilityPolicy.DefaultReservationEnabled);
    }

    [Fact]
    public void Create_WhenLotIdIsEmpty_ShouldThrowArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ReservationAvailabilityPolicy.Create(Guid.Empty, true));

        Assert.Equal("lotId", ex.ParamName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_WhenValid_ShouldSetPropertiesCorrectly(bool enabled)
    {
        var lotId = Guid.NewGuid();
        var policy = ReservationAvailabilityPolicy.Create(lotId, enabled);

        Assert.NotNull(policy);
        Assert.NotEqual(Guid.Empty, policy.Id);
        Assert.Equal(lotId, policy.LotId);
        Assert.Equal(enabled, policy.ReservationEnabled);
        Assert.True(policy.UpdatedAtUtc <= DateTime.UtcNow);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Update_ShouldChangeReservationEnabledAndTimestamp(bool initial, bool updated)
    {
        var lotId = Guid.NewGuid();
        var policy = ReservationAvailabilityPolicy.Create(lotId, initial);
        var initialTimestamp = policy.UpdatedAtUtc;

        policy.Update(updated);

        Assert.Equal(updated, policy.ReservationEnabled);
        Assert.True(policy.UpdatedAtUtc >= initialTimestamp);
    }
}
