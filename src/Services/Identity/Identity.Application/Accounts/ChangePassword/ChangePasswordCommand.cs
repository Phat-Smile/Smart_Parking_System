using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.ChangePassword;

public sealed record ChangePasswordCommand(Guid AccountId, string CurrentPassword, string NewPassword) : IRequest<Result<PasswordChangedResult>>;

public sealed record PasswordChangedResult(string Message);

public static class ChangePasswordErrors
{
    public static readonly Error InvalidCurrentPassword =
        new("identity.invalid_current_password", "Mật khẩu hiện tại không chính xác.");
}

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.CurrentPassword)
            .NotEmpty().WithMessage("Mật khẩu hiện tại là bắt buộc, không được để trống.")
            .Must(PasswordPolicy.FitsBcrypt).WithMessage(PasswordPolicy.TooLongMessage);

        RuleFor(command => command.NewPassword)
            .MeetsPasswordPolicy()
            .NotEqual(command => command.CurrentPassword, StringComparer.Ordinal).WithMessage(PasswordPolicy.SameAsCurrentMessage);
    }
}

// US-AUTH-009 (decided 2026-10-09): a wrong current password counts as a failed login, so three in a row lock the
// account for 15 minutes; success revokes every session and refresh token.
public sealed class ChangePasswordCommandHandler(
    IAccountRepository accounts,
    IAuthSessionRepository authSessions,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IProfileRepository profiles,
    IClock clock) : IRequestHandler<ChangePasswordCommand, Result<PasswordChangedResult>>
{
    public const string RevokedReason = "PasswordChanged";

    public async Task<Result<PasswordChangedResult>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByIdForUpdateAsync(request.AccountId, cancellationToken);
            if (account is null)
                return Result<PasswordChangedResult>.Failure(ChangePasswordErrors.InvalidCurrentPassword);

            if (account.Status == AccountStatus.Suspended)
                return Result<PasswordChangedResult>.Failure(LoginErrors.AccountSuspended);

            if (account.Status == AccountStatus.Disabled)
                return Result<PasswordChangedResult>.Failure(LoginErrors.AccountDisabled);

            if (account.IsLockedOut(now))
                return Result<PasswordChangedResult>.Failure(AccountMessages.Locked(account, now));

            account.ClearExpiredLockout(now);

            // An account created with Google has no password yet (G3): treated like a wrong one.
            var matched = false;
            if (account.PasswordHash is { } passwordHash)
                matched = passwordHasher.Verify(request.CurrentPassword, passwordHash);
            else
                passwordHasher.VerifyDummy(request.CurrentPassword);

            if (!matched)
            {
                account.RecordFailedLogin(now);
                await accounts.SaveChangesAsync(cancellationToken);
                if (account.IsLockedOut(now))
                    return Result<PasswordChangedResult>.Failure(AccountMessages.Locked(account, now));

                // Wrong current passwords count towards the same login lockout, so the user is warned before it happens.
                return Result<PasswordChangedResult>.Failure(ChangePasswordErrors.InvalidCurrentPassword with
                {
                    Message = $"Mật khẩu hiện tại không chính xác. Bạn còn {Math.Max(0, Account.MaxFailedAttemptsBeforeLockout - account.FailedLoginAttempts)} lần thử trước khi đăng nhập bị tạm khóa {Account.LockoutDurationMinutes} phút trên mọi thiết bị."
                });
            }

            // Checked after the current password matched, so a rejected new password never counts as a failed login.
            var profile = await profiles.FindByAccountIdAsync(account.Id, cancellationToken);
            if (PasswordPolicy.ContainsPersonalInfo(request.NewPassword, account.Email, profile?.FullName))
                return Result<PasswordChangedResult>.Failure(PasswordPolicy.PersonalInfoError);

            account.ResetPassword(passwordHasher.Hash(request.NewPassword));
            await authSessions.RevokeAllForAccountAsync(account.Id, now, RevokedReason, cancellationToken);
            await refreshTokens.RevokeAllForAccountAsync(account.Id, now, RevokedReason, cancellationToken);
            await accounts.SaveChangesAsync(cancellationToken);

            return Result<PasswordChangedResult>.Success(new PasswordChangedResult("Đổi mật khẩu thành công. Bạn đã được đăng xuất khỏi tất cả thiết bị, vui lòng đăng nhập lại bằng mật khẩu mới."));
        }, cancellationToken);
    }
}
