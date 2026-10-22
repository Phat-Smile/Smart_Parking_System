using System.Reflection;
using ParkingSystem.Identity.Application.Accounts.RegisterAccount;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests.Profiles;

// US-PRIV-002 AC3: Registration commands/contracts must never include citizen id (CCCD) or driver license fields.
public sealed class RegisterAccountContractTests
{
    private static readonly string[] ForbiddenFieldFragments =
    [
        "cccd",
        "citizen",
        "identitycard",
        "cmnd",
        "driverlicense",
        "drivinglicense",
        "gplx"
    ];

    [Fact]
    public void RegisterAccountCommand_contract_does_not_contain_cccd_or_driver_license()
    {
        var properties = typeof(RegisterAccountCommand)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name.ToLowerInvariant())
            .ToArray();

        foreach (var prop in properties)
        {
            foreach (var forbidden in ForbiddenFieldFragments)
            {
                Assert.DoesNotContain(forbidden, prop);
            }
        }
    }
}
