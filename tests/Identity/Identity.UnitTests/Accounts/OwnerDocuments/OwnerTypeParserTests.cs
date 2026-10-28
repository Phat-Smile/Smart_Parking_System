using ParkingSystem.Identity.Application.Accounts.OwnerApplications;
using ParkingSystem.Identity.Domain.Accounts;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Accounts.OwnerDocuments;

public sealed class OwnerTypeParserTests
{
    [Theory]
    [InlineData("Individual", OwnerType.Individual)]
    [InlineData("individual", OwnerType.Individual)]
    [InlineData("HOUSEHOLDBUSINESS", OwnerType.HouseholdBusiness)]
    [InlineData("  Enterprise  ", OwnerType.Enterprise)]
    public void A_named_value_is_accepted_in_any_casing(string input, OwnerType expected)
    {
        Assert.True(OwnerTypeParser.TryParse(input, out var parsed));
        Assert.Equal(expected, parsed);
    }

    // Enum.TryParse would accept "1" or "99"; only the names are part of the contract.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("99")]
    [InlineData("Household")]
    [InlineData("Individual,Enterprise")]
    public void Numbers_partial_names_and_blanks_are_rejected(string? input)
    {
        Assert.False(OwnerTypeParser.TryParse(input, out _));
    }
}
