using System.Text;

namespace ParkingSystem.Identity.Application.Accounts;

public static class PasswordPolicy
{
    // BCrypt (BCrypt.Net-Next, non-enhanced) only uses the first 72 bytes: a longer UTF-8 password would be
    // silently truncated, so two different passwords sharing those bytes would both log in.
    public const int MaxUtf8Bytes = 72;

    public const string TooLongMessage = "Password must not exceed 72 bytes in UTF-8.";

    public static bool FitsBcrypt(string? password) =>
        password is null || Encoding.UTF8.GetByteCount(password) <= MaxUtf8Bytes;
}
