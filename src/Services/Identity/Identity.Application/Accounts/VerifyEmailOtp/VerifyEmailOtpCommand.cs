using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.EmailVerifications;

namespace ParkingSystem.Identity.Application.Accounts.VerifyEmailOtp;

public sealed record VerifyEmailOtpCommand(string Email, string OtpCode)
    : IRequest<Result<VerifiedEmailAccount>>;

public sealed record VerifiedEmailAccount(Guid AccountId, string Email, string Status, bool IsVerified = true);

public static class VerifyEmailOtpErrors
{
    public static readonly Error InvalidOtpCode =
        new("identity.invalid_otp_code", "Incorrect OTP code. Please check and try again.");

    public static readonly Error OtpExpired =
        new("identity.otp_expired", "OTP code is invalid or has expired. Please request a new code.");

    public static readonly Error OtpMaxAttemptsExceeded =
        new("identity.otp_max_attempts_exceeded", "Maximum OTP verification attempts exceeded. Please request a new OTP code.");
}

public sealed class VerifyEmailOtpCommandValidator : AbstractValidator<VerifyEmailOtpCommand>
{
    public VerifyEmailOtpCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(254)
            .EmailAddress();

        RuleFor(command => command.OtpCode)
            .NotEmpty()
            .Matches("^[0-9]{6}$") // ASCII digits only: \d also matches other Unicode digits.
            .WithMessage("otpCode must be a 6-digit number.");
    }
}

public sealed class VerifyEmailOtpCommandHandler(
    IAccountRepository accounts,
    IEmailVerificationRepository emailVerifications,
    IOtpHasher otpHasher,
    IOutboxWriter outboxWriter,
    IClock clock) : IRequestHandler<VerifyEmailOtpCommand, Result<VerifiedEmailAccount>>
{
    public async Task<Result<VerifiedEmailAccount>> Handle(VerifyEmailOtpCommand request, CancellationToken cancellationToken)
    {
        var email = Account.NormalizeEmail(request.Email);

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            // Acquire row-level lock on the account record to serialize with concurrent Resend and Verify operations
            var account = await accounts.FindByEmailForUpdateAsync(email, cancellationToken);

            // Security: Avoid account enumeration. If account does not exist or is not in PendingEmailVerification,
            // return generic expired error.
            if (account is null || account.Status != AccountStatus.PendingEmailVerification)
                return Result<VerifiedEmailAccount>.Failure(VerifyEmailOtpErrors.OtpExpired);

            var now = clock.UtcNow;
            // Read the latest unused code even when wrong attempts invalidated it: until a new code is requested,
            // every later try keeps answering otp_max_attempts_exceeded (US-AUTH-003 spec).
            var verification = await emailVerifications.GetLatestUnusedForAccountAsync(account.Id, cancellationToken);
            if (verification is null)
                return Result<VerifiedEmailAccount>.Failure(VerifyEmailOtpErrors.OtpExpired);

            if (verification.IsExhausted)
                return Result<VerifiedEmailAccount>.Failure(VerifyEmailOtpErrors.OtpMaxAttemptsExceeded);

            if (verification.IsInvalidated || verification.IsExpired(now))
                return Result<VerifiedEmailAccount>.Failure(VerifyEmailOtpErrors.OtpExpired);

            var matched = verification.RecordAttempt(request.OtpCode, otpHasher, now);

            if (!matched)
            {
                await emailVerifications.SaveChangesAsync(cancellationToken);

                return verification.IsExhausted
                    ? Result<VerifiedEmailAccount>.Failure(VerifyEmailOtpErrors.OtpMaxAttemptsExceeded)
                    : Result<VerifiedEmailAccount>.Failure(VerifyEmailOtpErrors.InvalidOtpCode);
            }

            var transitioned = account.VerifyEmail();

            if (transitioned)
            {
                outboxWriter.Enqueue(
                    new AccountEmailVerifiedIntegrationEvent(account.Id, account.Email, now),
                    "identity.account-email-verified.v1",
                    1);
            }

            await emailVerifications.SaveChangesAsync(cancellationToken);

            return Result<VerifiedEmailAccount>.Success(new VerifiedEmailAccount(account.Id, account.Email, account.Status.ToString(), true));
        }, cancellationToken);
    }
}
