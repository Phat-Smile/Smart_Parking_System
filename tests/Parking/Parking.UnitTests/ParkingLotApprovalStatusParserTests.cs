using ParkingSystem.Parking.Application.ParkingLots.AdminLots;
using ParkingSystem.Parking.Domain.ParkingLots;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

// The admin list filter accepts a status NAME only. Enum.TryParse also took numbers and comma separated lists, so
// "Pending,Rejected" meant Rejected (0 | 2) and "99" meant nothing, both answered with 200.
public sealed class ParkingLotApprovalStatusParserTests
{
    [Theory]
    [InlineData("Pending", ParkingLotApprovalStatus.Pending)]
    [InlineData("approved", ParkingLotApprovalStatus.Approved)]
    [InlineData("REJECTED", ParkingLotApprovalStatus.Rejected)]
    [InlineData("  Approved  ", ParkingLotApprovalStatus.Approved)]
    public void A_status_name_is_accepted_without_case_sensitivity_or_surrounding_spaces(string value, ParkingLotApprovalStatus expected)
    {
        Assert.True(ParkingLotApprovalStatusParser.TryParse(value, out var status));
        Assert.Equal(expected, status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("99")]
    [InlineData("-1")]
    [InlineData("Pending,Rejected")]
    [InlineData("Approved,")]
    [InlineData("Pending;Rejected")]
    [InlineData("Pending Approved")]
    [InlineData("Nope")]
    public void Anything_that_is_not_a_status_name_is_refused(string? value)
    {
        Assert.False(ParkingLotApprovalStatusParser.TryParse(value, out _));
    }
}
