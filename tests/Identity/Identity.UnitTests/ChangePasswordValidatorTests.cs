using ParkingSystem.Identity.Application.Accounts.ChangePassword;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class ChangePasswordValidatorTests
{
    private readonly ChangePasswordCommandValidator _validator = new();

    [Theory]
    [InlineData("Abc@12345", "Xyz@67890", true)]
    [InlineData("", "Xyz@67890", false)]                              // current password required
    [InlineData("Abc@12345", "weak", false)]                          // new password below the policy
    [InlineData("Abc@12345", "Aa1@ầầầầầầầầầầầầầầầầầầầầầầầầ", false)] // 79 bytes in UTF-8
    public void Validates_both_passwords(string current, string newPassword, bool expectedValid)
    {
        var result = _validator.Validate(new ChangePasswordCommand(Guid.NewGuid(), current, newPassword));
        Assert.Equal(expectedValid, result.IsValid);
    }
}
