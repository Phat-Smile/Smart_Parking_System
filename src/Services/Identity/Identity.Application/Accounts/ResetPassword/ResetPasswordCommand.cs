using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts.VerifyEmailOtp;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.EmailVerifications;

namespace ParkingSystem.Identity.Application.Accounts.ResetPassword;

public sealed record ResetPasswordCommand(string Email, string OtpCode, string NewPassword) : IRequest<Result<PasswordResetResult>>;

public sealed record PasswordResetResult(string Message);

public static class ResetPasswordErrors
{
    public static readonly Error OtpExpired =
        new("identity.otp_expired", "Mã OTP đặt lại mật khẩu đã hết hạn hoặc không còn hiệu lực. Vui lòng yêu cầu mã mới.");

    public static readonly Error InvalidOtpCode =
        new("identity.invalid_otp_code", "Mã OTP đặt lại mật khẩu không chính xác. Vui lòng kiểm tra lại.");

    public static readonly Error OtpMaxAttemptsExceeded =
        new("identity.otp_max_attempts_exceeded", $"Bạn đã nhập sai mã OTP {EmailVerification.MaxAttempts} lần nên mã này không còn dùng được. Vui lòng gửi lại yêu cầu đặt lại mật khẩu.");

    public static readonly Error SameAsCurrentPassword =
        new("identity.password_same_as_current", PasswordPolicy.SameAsCurrentMessage);
}

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email là bắt buộc, không được để trống.")
            .MaximumLength(254).WithMessage("Email không được vượt quá 254 ký tự.")
            .EmailAddress().WithMessage("Email không đúng định dạng hợp lệ.");

        RuleFor(command => command.OtpCode)
            .NotEmpty().WithMessage("Mã OTP là bắt buộc, không được để trống.")
            .Matches("^[0-9]{6}$") // ASCII digits only: \d also matches other Unicode digits.
            .WithMessage("Mã OTP phải là một dãy số gồm đúng 6 chữ số.");

        RuleFor(command => command.NewPassword).MeetsPasswordPolicy();
    }
}

// US-AUTH-006 (C1, R-LOCK): same code rules as email verification; on success the password changes, every session
// and refresh token is revoked and the login lockout is cleared, all in one transaction.
public sealed class ResetPasswordCommandHandler(
    IAccountRepository accounts,
    IEmailVerificationRepository codes,
    IAuthSessionRepository authSessions,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IOtpHasher otpHasher,
    IClock clock) : IRequestHandler<ResetPasswordCommand, Result<PasswordResetResult>>
{
    public const string RevokedReason = "PasswordReset";
    private const OtpPurpose Purpose = OtpPurpose.PasswordReset;

    public async Task<Result<PasswordResetResult>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = Account.NormalizeEmail(request.Email);

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByEmailForUpdateAsync(email, cancellationToken);
            if (account is null || account.Status != AccountStatus.EmailVerified)
                return Result<PasswordResetResult>.Failure(ResetPasswordErrors.OtpExpired);

            var now = clock.UtcNow;
            var code = await codes.GetLatestUnusedForAccountAsync(account.Id, cancellationToken, Purpose);
            if (code is null)
                return Result<PasswordResetResult>.Failure(ResetPasswordErrors.OtpExpired);

            if (code.IsExhausted)
                return Result<PasswordResetResult>.Failure(ResetPasswordErrors.OtpMaxAttemptsExceeded);

            if (code.IsInvalidated)
                return Result<PasswordResetResult>.Failure(AccountMessages.OtpReplaced);

            if (code.IsExpired(now))
                return Result<PasswordResetResult>.Failure(ResetPasswordErrors.OtpExpired);

            if (!code.RecordAttempt(request.OtpCode, otpHasher, now))
            {
                // Committed even though the request fails: the attempt count is what stops brute force.
                await codes.SaveChangesAsync(cancellationToken);
                return code.IsExhausted
                    ? Result<PasswordResetResult>.Failure(ResetPasswordErrors.OtpMaxAttemptsExceeded)
                    : Result<PasswordResetResult>.Failure(AccountMessages.InvalidOtp(code, "Mã OTP đặt lại mật khẩu"));
            }

            // Checked only after the code matched, so it cannot be used to guess the current password. Nothing is
            // saved here: the code stays usable and the user can retry at once with a different password.
            if (account.PasswordHash is { } currentHash && passwordHasher.Verify(request.NewPassword, currentHash))
                return Result<PasswordResetResult>.Failure(ResetPasswordErrors.SameAsCurrentPassword);

            account.ResetPassword(passwordHasher.Hash(request.NewPassword));
            await authSessions.RevokeAllForAccountAsync(account.Id, now, RevokedReason, cancellationToken);
            await refreshTokens.RevokeAllForAccountAsync(account.Id, now, RevokedReason, cancellationToken);
            await accounts.SaveChangesAsync(cancellationToken);

            return Result<PasswordResetResult>.Success(new PasswordResetResult("Đặt lại mật khẩu thành công. Bạn đã được đăng xuất khỏi tất cả thiết bị, vui lòng đăng nhập bằng mật khẩu mới."));
        }, cancellationToken);
    }
}
