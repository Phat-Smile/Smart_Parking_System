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

// OtpSent is false when no code was issued (unknown email or account already verified); the timers are then 0.
public sealed record ResentOtpResult(string Email, int ExpiresInSeconds, int CooldownSeconds, string Message, bool OtpSent);

public static class ResendOtpErrors
{
    // Decided 2026-10-09: registration already answers 409 for a known email, so resend tells the user plainly
    // what happened instead of a hedged "if the account exists" message.
    public const string EmailNotRegisteredMessage = "Email này chưa được đăng ký. Vui lòng đăng ký tài khoản.";
    public const string AlreadyVerifiedMessage = "Tài khoản này đã được xác thực trước đó. Vui lòng đăng nhập.";

    public static string SentMessage(string email) =>
        $"Đã gửi mã OTP mới đến {email}. Mã có hiệu lực {EmailVerification.ExpirationMinutes} phút; bạn có thể yêu cầu gửi lại sau {EmailVerification.CooldownSeconds} giây.";

    public const string CooldownActiveCode = "identity.resend_cooldown_active";
    public const string RateLimitExceededCode = "identity.resend_rate_limit_exceeded";

    public static Error CooldownActive(int retryAfterSeconds) =>
        new(CooldownActiveCode, $"Vui lòng đợi {retryAfterSeconds} giây trước khi yêu cầu mã mới. Mã đã gửi trước đó vẫn dùng được nếu chưa hết hạn.")
        {
            RetryAfterSeconds = retryAfterSeconds
        };

    public static Error RateLimitExceeded(int retryAfterSeconds) =>
        new(RateLimitExceededCode, $"Email này đã nhận tối đa {EmailVerification.MaxHourlyOtpIssued} mã OTP trong 1 giờ (tính cả mã gửi khi đăng ký). Vui lòng thử lại sau {(retryAfterSeconds + 59) / 60} phút.")
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
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email là bắt buộc, không được để trống.")
            .MaximumLength(254).WithMessage("Email không được vượt quá 254 ký tự.")
            .EmailAddress().WithMessage("Email không đúng định dạng hợp lệ.");
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

            // No code is issued for an unknown email or an account that no longer waits for verification.
            if (account is null)
                return Result<ResentOtpResult>.Success(new ResentOtpResult(email, 0, 0, ResendOtpErrors.EmailNotRegisteredMessage, false));

            if (account.Status != AccountStatus.PendingEmailVerification)
                return Result<ResentOtpResult>.Success(new ResentOtpResult(email, 0, 0, ResendOtpErrors.AlreadyVerifiedMessage, false));

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
                ResendOtpErrors.SentMessage(email),
                true));
        }, cancellationToken);
    }
}
