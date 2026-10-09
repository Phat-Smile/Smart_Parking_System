using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Accounts.VerifyEmailOtp;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.EmailVerifications;

namespace ParkingSystem.Identity.Application.Accounts;

// Messages that depend on the account or code state, so the user is told what actually happened and what to do next.
// The error codes stay the static ones in LoginErrors / VerifyEmailOtpErrors.
public static class AccountMessages
{
    public const string ContactAdmin = "Vui lòng liên hệ quản trị viên để được hỗ trợ.";

    public static int MinutesUntil(DateTimeOffset endUtc, DateTimeOffset nowUtc) =>
        Math.Max(1, (int)Math.Ceiling((endUtc - nowUtc).TotalMinutes));

    public static Error InvalidCredentials(Account account) =>
        LoginErrors.InvalidCredentials with
        {
            Message = $"Email hoặc mật khẩu không chính xác. Bạn còn {Math.Max(0, Account.MaxFailedAttemptsBeforeLockout - account.FailedLoginAttempts)} lần thử trước khi tài khoản bị tạm khóa {Account.LockoutDurationMinutes} phút."
        };

    public static Error Locked(Account account, DateTimeOffset nowUtc) =>
        LoginErrors.AccountTemporarilyLocked with
        {
            Message = $"Tài khoản đang tạm khóa do nhập sai mật khẩu {Account.MaxFailedAttemptsBeforeLockout} lần liên tiếp. " +
                      $"Vui lòng thử lại sau {MinutesUntil(account.LockoutEndUtc ?? nowUtc, nowUtc)} phút, hoặc dùng \"Quên mật khẩu\" để mở khóa ngay.",
            RetryAfterSeconds = Math.Max(1, (int)Math.Ceiling(((account.LockoutEndUtc ?? nowUtc) - nowUtc).TotalSeconds))
        };

    public static Error InvalidOtp(EmailVerification code, string codeName = "Mã OTP") =>
        VerifyEmailOtpErrors.InvalidOtpCode with
        {
            Message = $"{codeName} không đúng. Bạn còn {Math.Max(0, EmailVerification.MaxAttempts - code.AttemptCount)} lần thử."
        };

    public static Error OtpReplaced { get; } = VerifyEmailOtpErrors.OtpExpired with
    {
        Message = "Mã OTP này đã được thay bằng mã mới hơn. Vui lòng dùng mã trong email gần nhất."
    };
}
