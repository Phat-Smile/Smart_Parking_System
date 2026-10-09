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

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(254)
            .EmailAddress();

        RuleFor(command => command.OtpCode)
            .NotEmpty()
            .Matches("^[0-9]{6}$") // ASCII digits only: \d also matches other Unicode digits.
            .WithMessage("otpCode must be a 6-digit number.");

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
                return Result<PasswordResetResult>.Failure(VerifyEmailOtpErrors.OtpExpired);

            var now = clock.UtcNow;
            var code = await codes.GetLatestUnusedForAccountAsync(account.Id, cancellationToken, Purpose);
            if (code is null)
                return Result<PasswordResetResult>.Failure(VerifyEmailOtpErrors.OtpExpired);

            if (code.IsExhausted)
                return Result<PasswordResetResult>.Failure(VerifyEmailOtpErrors.OtpMaxAttemptsExceeded);

            if (code.IsInvalidated || code.IsExpired(now))
                return Result<PasswordResetResult>.Failure(VerifyEmailOtpErrors.OtpExpired);

            if (!code.RecordAttempt(request.OtpCode, otpHasher, now))
            {
                // Committed even though the request fails: the attempt count is what stops brute force.
                await codes.SaveChangesAsync(cancellationToken);
                return code.IsExhausted
                    ? Result<PasswordResetResult>.Failure(VerifyEmailOtpErrors.OtpMaxAttemptsExceeded)
                    : Result<PasswordResetResult>.Failure(VerifyEmailOtpErrors.InvalidOtpCode);
            }

            account.ResetPassword(passwordHasher.Hash(request.NewPassword));
            await authSessions.RevokeAllForAccountAsync(account.Id, now, RevokedReason, cancellationToken);
            await refreshTokens.RevokeAllForAccountAsync(account.Id, now, RevokedReason, cancellationToken);
            await accounts.SaveChangesAsync(cancellationToken);

            return Result<PasswordResetResult>.Success(new PasswordResetResult("Password has been reset. Please log in with the new password."));
        }, cancellationToken);
    }
}
