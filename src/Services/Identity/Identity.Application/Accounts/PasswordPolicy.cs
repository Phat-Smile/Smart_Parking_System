using System.Text;
using FluentValidation;

namespace ParkingSystem.Identity.Application.Accounts;

public static class PasswordPolicy
{
    // BCrypt (BCrypt.Net-Next, non-enhanced) only uses the first 72 bytes: a longer UTF-8 password would be
    // silently truncated, so two different passwords sharing those bytes would both log in.
    public const int MaxUtf8Bytes = 72;

    public const string TooLongMessage = "Password must not exceed 72 bytes in UTF-8.";

    public static bool FitsBcrypt(string? password) =>
        password is null || Encoding.UTF8.GetByteCount(password) <= MaxUtf8Bytes;

    // Context 6.1: at least 8 characters with upper case, lower case, digit and special character; at most 72 bytes.
    public static IRuleBuilderOptions<T, string> MeetsPasswordPolicy<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8)
            .Must(FitsBcrypt).WithMessage(TooLongMessage)
            .Matches("[A-Z]").WithMessage("Password must contain an upper-case letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lower-case letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .Matches("[^A-Za-z0-9]").WithMessage("Password must contain a special character.");
}
