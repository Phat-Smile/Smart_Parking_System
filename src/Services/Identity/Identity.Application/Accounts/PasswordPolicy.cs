using System.Text;
using FluentValidation;

namespace ParkingSystem.Identity.Application.Accounts;

public static class PasswordPolicy
{
    // BCrypt (BCrypt.Net-Next, non-enhanced) only uses the first 72 bytes: a longer UTF-8 password would be
    // silently truncated, so two different passwords sharing those bytes would both log in.
    public const int MaxUtf8Bytes = 72;

    public const string TooLongMessage = "Mật khẩu quá dài: tối đa 72 ký tự không dấu (ký tự có dấu chiếm nhiều chỗ hơn).";

    public const string SameAsCurrentMessage = "Mật khẩu mới phải khác mật khẩu hiện tại.";

    public static bool FitsBcrypt(string? password) =>
        password is null || Encoding.UTF8.GetByteCount(password) <= MaxUtf8Bytes;

    // Context 6.1: at least 8 characters with upper case, lower case, digit and special character; at most 72 bytes.
    public static IRuleBuilderOptions<T, string> MeetsPasswordPolicy<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Mật khẩu là bắt buộc, không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự.")
            .Must(FitsBcrypt).WithMessage(TooLongMessage)
            .Matches("[A-Z]").WithMessage("Mật khẩu phải chứa ít nhất một chữ cái viết hoa.")
            .Matches("[a-z]").WithMessage("Mật khẩu phải chứa ít nhất một chữ cái viết thường.")
            .Matches("[0-9]").WithMessage("Mật khẩu phải chứa ít nhất một chữ số.")
            .Matches("[^A-Za-z0-9]").WithMessage("Mật khẩu phải chứa ít nhất một ký tự đặc biệt.");
}
