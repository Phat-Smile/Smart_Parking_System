using ParkingSystem.Identity.Application.Accounts.ForgotPassword;
using ParkingSystem.Identity.Application.Accounts.ResetPassword;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

public sealed class PasswordResetValidatorTests
{
    private readonly ResetPasswordCommandValidator _reset = new();
    private readonly ForgotPasswordCommandValidator _forgot = new();

    [Theory]
    [InlineData("user@example.com", true)]
    [InlineData("not-an-email", false)]
    [InlineData("", false)]
    public void Forgot_password_requires_a_valid_email(string email, bool expectedValid)
    {
        Assert.Equal(expectedValid, _forgot.Validate(new ForgotPasswordCommand(email)).IsValid);
    }

    [Theory]
    [InlineData("123456", true)]
    [InlineData("12345", false)]
    [InlineData("12a456", false)]
    [InlineData("١٢٣٤٥٦", false)] // Unicode digits are not a code
    public void Reset_code_is_six_ascii_digits(string code, bool expectedValid)
    {
        var result = _reset.Validate(new ResetPasswordCommand("user@example.com", code, "Xyz@67890"));
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData("Xyz@67890", true)]
    [InlineData("Sh#1a", false)]                          // shorter than 8
    [InlineData("secret#123", false)]                     // no upper case
    [InlineData("Secret1234", false)]                     // no special character
    [InlineData("Aa1@ầầầầầầầầầầầầầầầầầầầầầầầầ", false)] // 79 bytes in UTF-8
    public void New_password_follows_the_registration_policy(string password, bool expectedValid)
    {
        var result = _reset.Validate(new ResetPasswordCommand("user@example.com", "123456", password));
        Assert.Equal(expectedValid, result.IsValid);
    }
}
