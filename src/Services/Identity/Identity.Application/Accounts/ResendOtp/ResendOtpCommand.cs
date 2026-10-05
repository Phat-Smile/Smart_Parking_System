using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.BuildingBlocks.Events;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.EmailVerifications;

namespace ParkingSystem.Identity.Application.Accounts.ResendOtp;

public sealed record ResendOtpCommand(string Email) : IRequest<Result<ResentOtpResult>>;

public sealed record ResentOtpResult(string Email, int ExpiresInSeconds, int CooldownSeconds, string Message);

public static class ResendOtpErrors
{
    public static readonly Error CooldownActive =
        new("identity.resend_cooldown_active", "Please wait at least 60 seconds before requesting another verification code.");

    public static readonly Error RateLimitExceeded =
        new("identity.resend_rate_limit_exceeded", "Maximum resend limit of 5 OTPs per hour reached for this email.");
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
    IOutboxWriter outboxWriter,
    IClock clock) : IRequestHandler<ResendOtpCommand, Result<ResentOtpResult>>
{
    public async Task<Result<ResentOtpResult>> Handle(ResendOtpCommand request, CancellationToken cancellationToken)
    {
        var email = Account.NormalizeEmail(request.Email);
        var account = await accounts.FindByEmailAsync(email, cancellationToken);

        // Security: Avoid account enumeration. If account does not exist or is already active,
        // return generic success response without issuing an OTP.
        if (account is null || account.Status == AccountStatus.EmailVerified)
        {
            return Result<ResentOtpResult>.Success(new ResentOtpResult(
                email,
                EmailVerification.ExpirationMinutes * 60,
                EmailVerification.CooldownSeconds,
                "Verification code has been resent to your email."));
        }

        var now = clock.UtcNow;

        // Check 60s cooldown
        var cooldownThreshold = now.AddSeconds(-EmailVerification.CooldownSeconds);
        var inCooldown = await emailVerifications.GetLatestResendInCooldownAsync(email, cooldownThreshold, cancellationToken);
        if (inCooldown is not null)
            return Result<ResentOtpResult>.Failure(ResendOtpErrors.CooldownActive);

        // Check hourly rate limit (max 5 resends per hour)
        var hourlyThreshold = now.AddHours(-1);
        var hourlyCount = await emailVerifications.CountRecentResendsInPastHourAsync(email, hourlyThreshold, cancellationToken);
        if (hourlyCount >= EmailVerification.MaxHourlyResends)
            return Result<ResentOtpResult>.Failure(ResendOtpErrors.RateLimitExceeded);

        // Invalidate active OTP
        var previousActive = await emailVerifications.GetLatestActiveForAccountAsync(account.Id, cancellationToken);
        previousActive?.Invalidate();

        // Issue new OTP
        var otpCode = otpGenerator.GenerateNumericOtp(EmailVerification.OtpLength);
        var newVerification = EmailVerification.Issue(Guid.NewGuid(), account.Id, email, otpCode, now);
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
    }
}
