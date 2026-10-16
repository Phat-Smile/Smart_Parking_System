using System.Globalization;
using System.Text;
using FluentValidation;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Identity.Application.Accounts;

public static class PasswordPolicy
{
    // BCrypt (BCrypt.Net-Next, non-enhanced) only uses the first 72 bytes: a longer UTF-8 password would be
    // silently truncated, so two different passwords sharing those bytes would both log in.
    public const int MaxUtf8Bytes = 72;

    // Shorter name parts and email local parts ("an", "van", "a1") would reject too many unrelated passwords.
    private const int MinPersonalTokenLength = 4;

    public const string TooLongMessage = "Mật khẩu quá dài: tối đa 72 ký tự không dấu (ký tự có dấu chiếm nhiều chỗ hơn).";

    public const string SameAsCurrentMessage = "Mật khẩu mới phải khác mật khẩu hiện tại.";

    public const string EdgeWhitespaceMessage = "Mật khẩu không được bắt đầu hoặc kết thúc bằng khoảng trắng.";

    public const string CommonPasswordMessage = "Mật khẩu này quá phổ biến và dễ bị đoán. Vui lòng chọn mật khẩu khác.";

    public const string PersonalInfoMessage = "Mật khẩu không được chứa họ tên hoặc tên tài khoản email của bạn.";

    public static readonly Error PersonalInfoError = new("identity.password_contains_personal_info", PersonalInfoMessage);

    // Passwords that satisfy the composition rules yet top public leak lists; compared case-insensitively.
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "P@ssw0rd", "P@ssw0rd1", "P@ssw0rd123", "P@ssword1", "P@ssword123", "Passw0rd!", "Passw0rd@",
        "Password1!", "Password1@", "Password@1", "Password@12", "Password@123", "Password@1234", "Password123!",
        "Password123@", "Password!1", "Pass@123", "Pass@1234", "Pass@12345", "Admin@123", "Admin@1234",
        "Admin@12345", "Admin123!", "Admin123@", "Abcd@1234", "Abcd@123", "Abc@1234", "Abc123!@#", "Aa@123456",
        "Aa123456@", "Aa@12345", "Qwerty@123", "Qwerty123!", "Qwerty@1234", "Welcome@1", "Welcome@123",
        "Welcome1!", "Test@123", "Test@1234", "Abc@123456", "Abcd1234!", "Iloveyou@1", "Matkhau@123", "Matkhau123@",
        "Changeme@1", "Letmein@1", "Zaq1@wsx", "1qaz@WSX", "Qwe@1234", "Asd@1234"
    };

    public static bool FitsBcrypt(string? password) =>
        password is null || Encoding.UTF8.GetByteCount(password) <= MaxUtf8Bytes;

    public static bool HasEdgeWhitespace(string? password) =>
        password is { Length: > 0 } && (char.IsWhiteSpace(password[0]) || char.IsWhiteSpace(password[^1]));

    public static bool IsCommon(string? password) => password is not null && CommonPasswords.Contains(password);

    // Context 6.1: at least 8 characters with upper case, lower case, digit and special character; at most 72 bytes.
    // Whitespace is allowed inside the password but never counts as the special character.
    public static IRuleBuilderOptions<T, string> MeetsPasswordPolicy<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Mật khẩu là bắt buộc, không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có ít nhất 8 ký tự.")
            .Must(FitsBcrypt).WithMessage(TooLongMessage)
            .Must(password => !HasEdgeWhitespace(password)).WithMessage(EdgeWhitespaceMessage)
            .Matches("[A-Z]").WithMessage("Mật khẩu phải chứa ít nhất một chữ cái viết hoa.")
            .Matches("[a-z]").WithMessage("Mật khẩu phải chứa ít nhất một chữ cái viết thường.")
            .Matches("[0-9]").WithMessage("Mật khẩu phải chứa ít nhất một chữ số.")
            .Matches(@"[^A-Za-z0-9\s]").WithMessage("Mật khẩu phải chứa ít nhất một ký tự đặc biệt.")
            .Must(password => !IsCommon(password)).WithMessage(CommonPasswordMessage);

    // Compared without diacritics, case or separators, so "NguyenVanHung@1" matches "Nguyễn Văn Hùng" and
    // "hung.nguyen@x.com" matches a password containing "hungnguyen" or "nguyen".
    public static bool ContainsPersonalInfo(string? password, string? email, string? fullName)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        var normalizedPassword = Fold(password);
        return PersonalTokens(email, fullName)
            .Any(token => token.Length >= MinPersonalTokenLength && normalizedPassword.Contains(token, StringComparison.Ordinal));
    }

    private static IEnumerable<string> PersonalTokens(string? email, string? fullName)
    {
        var at = email?.IndexOf('@') ?? -1;
        if (at > 0)
        {
            var localPart = email![..at];
            yield return Fold(localPart);
            foreach (var part in localPart.Split(['.', '_', '-', '+'], StringSplitOptions.RemoveEmptyEntries))
                yield return Fold(part);
        }

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            var parts = fullName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            yield return Fold(string.Concat(parts));
            foreach (var part in parts)
                yield return Fold(part);
        }
    }

    // Lower-case letters and digits only, Vietnamese diacritics removed (đ has no decomposition, so it is mapped).
    private static string Fold(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            var lower = char.ToLowerInvariant(c);
            if (lower == 'đ')
                builder.Append('d');
            else if (char.IsAsciiLetterOrDigit(lower))
                builder.Append(lower);
        }

        return builder.ToString();
    }
}
