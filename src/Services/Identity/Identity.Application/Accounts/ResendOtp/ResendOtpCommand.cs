using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.EmailVerifications;

namespace ParkingSystem.Identity.Application.Accounts.ResendOtp;

public sealed record ResendOtpCommand(string Email) : IRequest<Result<ResentOtpResult>>;

public sealed record ResentOtpResult(string Email, int ExpiresInSeconds, int CooldownSeconds, string Message);

public static class ResendOtpErrors
{
    public const string CooldownActiveCode = "identity.resend_cooldown_active";
    public const string RateLimitExceededCode = "identity.resend_rate_limit_exceeded";

    public static Error CooldownActive(int retryAfterSeconds) =>
        new(CooldownActiveCode, $"Please wait {retryAfterSeconds} seconds before requesting another verification code.")
        {
            RetryAfterSeconds = retryAfterSeconds
        };

    public static Error RateLimitExceeded(int retryAfterSeconds) =>
        new(RateLimitExceededCode, "Maximum resend limit of 5 OTPs per hour reached for this email.")
        {
            RetryAfterSeconds = retryAfterSeconds
        };

    // Whole seconds still to wait, never below 1 so a client never retries immediately.
    public static int SecondsUntil(DateTimeOffset readyAtUtc, DateTimeOffset nowUtc) =>
        Math.Max(1, (int)Math.Ceiling((readyAtUtc - nowUtc).TotalSeconds));
}

public sealed class ResendOtpCommandValidator : AbstractValidator<ResendOtpCommand>
{
    public ResendOtpCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(254)
            .EmailAddress();
    }
}

public sealed class ResendOtpCommandHandler(
    IAccountRepository accounts,
    IEmailVerificationRepository emailVerifications,
    IOtpGenerator otpGenerator,
    IOtpHasher otpHasher,
    IOutboxWriter outboxWriter,
    IClock clock) : IRequestHandler<ResendOtpCommand, Result<ResentOtpResult>>
{
    public async Task<Result<ResentOtpResult>> Handle(ResendOtpCommand request, CancellationToken cancellationToken)
    {
        var email = Account.NormalizeEmail(request.Email);

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            // Lock the account row with SELECT ... FOR UPDATE to serialize all concurrent Resend and Verify operations
            var account = await accounts.FindByEmailForUpdateAsync(email, cancellationToken);

            // Security: Avoid account enumeration. If account does not exist or is not in PendingEmailVerification,
            // return generic success response without issuing an OTP.
            if (account is null || account.Status != AccountStatus.PendingEmailVerification)
            {
                return Result<ResentOtpResult>.Success(new ResentOtpResult(
                    email,
                    EmailVerification.ExpirationMinutes * 60,
                    EmailVerification.CooldownSeconds,
                    "Verification code has been resent to your email."));
            }

            var now = clock.UtcNow;

            // Check 60s cooldown (must be evaluated AFTER acquiring the authoritative lock)
            var cooldownThreshold = now.AddSeconds(-EmailVerification.CooldownSeconds);
            var inCooldown = await emailVerifications.GetLatestResendInCooldownAsync(email, cooldownThreshold, cancellationToken);
            if (inCooldown is not null)
                return Result<ResentOtpResult>.Failure(ResendOtpErrors.CooldownActive(
                    ResendOtpErrors.SecondsUntil(inCooldown.CreatedAtUtc.AddSeconds(EmailVerification.CooldownSeconds), now)));

            // Check hourly rate limit (max 5 OTPs issued per hour)
            var hourlyThreshold = now.AddHours(-1);
            var hourlyCount = await emailVerifications.CountRecentResendsInPastHourAsync(email, hourlyThreshold, cancellationToken);
            if (hourlyCount >= EmailVerification.MaxHourlyOtpIssued)
            {
                var oldest = await emailVerifications.GetOldestIssuedSinceAsync(email, hourlyThreshold, cancellationToken);
                return Result<ResentOtpResult>.Failure(ResendOtpErrors.RateLimitExceeded(
                    ResendOtpErrors.SecondsUntil((oldest ?? now).AddHours(1), now)));
            }

            // Invalidate current active OTP
            var previousActive = await emailVerifications.GetLatestActiveForAccountAsync(account.Id, cancellationToken);
            previousActive?.Invalidate(now);

            // Issue new OTP with HMAC
            var otpCode = otpGenerator.GenerateNumericOtp(EmailVerification.OtpLength);
            var otpHash = otpHasher.HashOtp(otpCode, account.Id);
            var newVerification = EmailVerification.Issue(Guid.NewGuid(), account.Id, email, otpHash, now);
            emailVerifications.Add(newVerification);

            outboxWriter.Enqueue(
                new UserVerificationOtpIssuedIntegrationEvent(account.Id, email, otpCode, newVerification.ExpiresAtUtc),
                "identity.user-verification-otp-issued.v1",
                1);

            await emailVerifications.SaveChangesAsync(cancellationToken);

            return Result<ResentOtpResult>.Success(new ResentOtpResult(
                email,
                EmailVerification.ExpirationMinutes * 60,
                EmailVerification.CooldownSeconds,
                "Verification code has been resent to your email."));
        }, cancellationToken);
    }
}
