using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Accounts.ChangePassword;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Accounts.RegisterAccount;
using ParkingSystem.Identity.Application.Accounts.ResetPassword;
using ParkingSystem.Identity.Application.Accounts.VerifyEmailOtp;
using Xunit;

namespace ParkingSystem.Tests.Identity.UnitTests;

// Every field reports only the first rule it breaks, so the user gets one clear reason to fix.
public sealed class ValidationMessageTests
{
    [Theory]
    [InlineData("", "Mật khẩu là bắt buộc, không được để trống.")]
    [InlineData("abc", "Mật khẩu phải có ít nhất 8 ký tự.")]
    [InlineData("abcdefgh", "Mật khẩu phải chứa ít nhất một chữ cái viết hoa.")]
    [InlineData("Abcdefgh", "Mật khẩu phải chứa ít nhất một chữ số.")]
    [InlineData("Abcdefg1", "Mật khẩu phải chứa ít nhất một ký tự đặc biệt.")]
    [InlineData("12345678", "Mật khẩu phải chứa ít nhất một chữ cái viết hoa.")]
    [InlineData("Aa1@ầầầầầầầầầầầầầầầầầầầầầầầầ", PasswordPolicy.TooLongMessage)]
    [InlineData(" Abc@12345", PasswordPolicy.EdgeWhitespaceMessage)]
    [InlineData("Abc@12345	", PasswordPolicy.EdgeWhitespaceMessage)]
    [InlineData("Abc defg1", "Mật khẩu phải chứa ít nhất một ký tự đặc biệt.")]
    [InlineData("P@ssw0rd", PasswordPolicy.CommonPasswordMessage)]
    [InlineData("admin@123", "Mật khẩu phải chứa ít nhất một chữ cái viết hoa.")]
    [InlineData("aDMIN@1234", PasswordPolicy.CommonPasswordMessage)]
    public void Register_password_reports_only_the_first_broken_rule(string password, string expected)
    {
        var result = new RegisterAccountCommandValidator().Validate(new RegisterAccountCommand(null, "user@example.com", password, null));

        var error = Assert.Single(result.Errors);
        Assert.Equal(expected, error.ErrorMessage);
    }

    [Fact]
    public void Register_password_may_contain_inner_spaces()
    {
        var result = new RegisterAccountCommandValidator().Validate(new RegisterAccountCommand(null, "user@example.com", "Xe cua toi @1", null));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("hung123@gmail.com", null, "Hung123@gmail")]
    [InlineData("hung.nguyen@example.com", null, "Nguyen@2026x")]
    [InlineData("user@example.com", "Nguyễn Văn Hùng", "NguyenVanHung@1")]
    [InlineData("user@example.com", "Nguyễn Văn Hùng", "Hung@20262026")]
    [InlineData("user@example.com", "Đặng Thị Mai", "Dang#Secure9")]
    public void Register_rejects_a_password_containing_the_email_name_or_full_name(string email, string? fullName, string password)
    {
        var result = new RegisterAccountCommandValidator().Validate(new RegisterAccountCommand(fullName, email, password, null));

        var error = Assert.Single(result.Errors);
        Assert.Equal(PasswordPolicy.PersonalInfoMessage, error.ErrorMessage);
    }

    [Theory]
    [InlineData("an@example.com", "Lê Văn An", "Vanan#Secure9")]
    [InlineData("abc@example.com", null, "Abc@12345")]
    public void Register_ignores_personal_tokens_shorter_than_four_characters(string email, string? fullName, string password)
    {
        var result = new RegisterAccountCommandValidator().Validate(new RegisterAccountCommand(fullName, email, password, null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Personal_info_error_has_its_own_code()
    {
        Assert.Equal("identity.password_contains_personal_info", PasswordPolicy.PersonalInfoError.Code);
        Assert.False(PasswordPolicy.ContainsPersonalInfo("Xyz@67890", null, null));
    }

    [Theory]
    [InlineData("", "Email là bắt buộc, không được để trống.")]
    [InlineData("a", "Email không đúng định dạng hợp lệ.")]
    public void Login_email_reports_one_message(string email, string expected)
    {
        var result = new LoginCommandValidator().Validate(new LoginCommand(email, "any-password"));

        var error = Assert.Single(result.Errors);
        Assert.Equal(expected, error.ErrorMessage);
    }

    [Fact]
    public void Register_phone_message_describes_both_accepted_forms()
    {
        var result = new RegisterAccountCommandValidator().Validate(new RegisterAccountCommand(null, "user@example.com", "Abc@12345", "+8412"));

        var error = Assert.Single(result.Errors);
        Assert.Equal("Số điện thoại phải là số di động Việt Nam: 0xxxxxxxxx (10 chữ số) hoặc +84xxxxxxxxx, chữ số sau 0/+84 là 3, 5, 7, 8 hoặc 9.", error.ErrorMessage);
    }

    [Theory]
    [InlineData("12345", "Mã OTP phải là một dãy số gồm đúng 6 chữ số.")]
    [InlineData("", "Mã OTP là bắt buộc, không được để trống.")]
    public void Otp_code_reports_one_message(string otp, string expected)
    {
        var result = new VerifyEmailOtpCommandValidator().Validate(new VerifyEmailOtpCommand("user@example.com", otp));

        var error = Assert.Single(result.Errors);
        Assert.Equal(expected, error.ErrorMessage);
    }

    [Fact]
    public void Change_password_rejects_a_new_password_equal_to_the_current_one()
    {
        var result = new ChangePasswordCommandValidator().Validate(new ChangePasswordCommand(Guid.NewGuid(), "Abc@12345", "Abc@12345"));

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(ChangePasswordCommand.NewPassword), error.PropertyName);
        Assert.Equal("Mật khẩu mới phải khác mật khẩu hiện tại.", error.ErrorMessage);
    }

    [Fact]
    public void Change_password_compares_the_new_password_case_sensitively()
    {
        var result = new ChangePasswordCommandValidator().Validate(new ChangePasswordCommand(Guid.NewGuid(), "abc@12345A", "Abc@12345a"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Reset_password_same_password_error_has_its_own_code()
    {
        Assert.Equal("identity.password_same_as_current", ResetPasswordErrors.SameAsCurrentPassword.Code);
        Assert.Equal(PasswordPolicy.SameAsCurrentMessage, ResetPasswordErrors.SameAsCurrentPassword.Message);
    }
}
