using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Identity.Application.Accounts.ResendOtp;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.EmailVerifications;

namespace ParkingSystem.Identity.Application.Accounts.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : IRequest<Result<ForgotPasswordResult>>;

public sealed record ForgotPasswordResult(string Email, int ExpiresInSeconds, int CooldownSeconds, string Message);

public static class ForgotPasswordErrors
{
    public const string CooldownActiveCode = "identity.reset_cooldown_active";
    public const string RateLimitExceededCode = "identity.reset_rate_limit_exceeded";

    public static Error CooldownActive(int retryAfterSeconds) =>
        new(CooldownActiveCode, $"Please wait {retryAfterSeconds} seconds before requesting another code.")
        {
            RetryAfterSeconds = retryAfterSeconds
        };

    public static Error RateLimitExceeded(int retryAfterSeconds) =>
        new(RateLimitExceededCode, "Maximum of 5 password reset codes per hour reached for this email.")
        {
            RetryAfterSeconds = retryAfterSeconds
        };
}

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(254)
            .EmailAddress();
    }
}

// US-AUTH-006 (C1, R-ELIG): only EmailVerified accounts receive a code; every other email gets the same 200.
public sealed class ForgotPasswordCommandHandler(
    IAccountRepository accounts,
    IEmailVerificationRepository codes,
    IOtpGenerator otpGenerator,
    IOtpHasher otpHasher,
    IOutboxWriter outboxWriter,
    IClock clock) : IRequestHandler<ForgotPasswordCommand, Result<ForgotPasswordResult>>
{
    private const OtpPurpose Purpose = OtpPurpose.PasswordReset;

    public async Task<Result<ForgotPasswordResult>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = Account.NormalizeEmail(request.Email);
        var generic = new ForgotPasswordResult(
            email,
            EmailVerification.ExpirationMinutes * 60,
            EmailVerification.CooldownSeconds,
            "If the email belongs to an account, a password reset code has been sent.");

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            // Same lock as Verify / Resend / Reset so concurrent requests for one account are serialised.
            var account = await accounts.FindByEmailForUpdateAsync(email, cancellationToken);
            if (account is null || account.Status != AccountStatus.EmailVerified)
                return Result<ForgotPasswordResult>.Success(generic);

            var now = clock.UtcNow;
            var inCooldown = await codes.GetLatestResendInCooldownAsync(email, now.AddSeconds(-EmailVerification.CooldownSeconds), cancellationToken, Purpose);
            if (inCooldown is not null)
                return Result<ForgotPasswordResult>.Failure(ForgotPasswordErrors.CooldownActive(
                    ResendOtpErrors.SecondsUntil(inCooldown.CreatedAtUtc.AddSeconds(EmailVerification.CooldownSeconds), now)));

            var hourlyThreshold = now.AddHours(-1);
            var issuedThisHour = await codes.CountRecentResendsInPastHourAsync(email, hourlyThreshold, cancellationToken, Purpose);
            if (issuedThisHour >= EmailVerification.MaxHourlyOtpIssued)
            {
                var oldest = await codes.GetOldestIssuedSinceAsync(email, hourlyThreshold, cancellationToken, Purpose);
                return Result<ForgotPasswordResult>.Failure(ForgotPasswordErrors.RateLimitExceeded(
                    ResendOtpErrors.SecondsUntil((oldest ?? now).AddHours(1), now)));
            }

            var previous = await codes.GetLatestActiveForAccountAsync(account.Id, cancellationToken, Purpose);
            previous?.Invalidate(now);

            var otpCode = otpGenerator.GenerateNumericOtp(EmailVerification.OtpLength);
            var code = EmailVerification.Issue(Guid.NewGuid(), account.Id, email, otpHasher.HashOtp(otpCode, account.Id), now, Purpose);
            codes.Add(code);

            outboxWriter.Enqueue(
                new PasswordResetOtpIssuedIntegrationEvent(account.Id, email, otpCode, code.ExpiresAtUtc),
                "identity.password-reset-otp-issued.v1",
                1);

            await codes.SaveChangesAsync(cancellationToken);
            return Result<ForgotPasswordResult>.Success(generic);
        }, cancellationToken);
    }
}
