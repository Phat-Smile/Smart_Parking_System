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

public sealed record VerifiedEmailAccount(Guid AccountId, string Email, string Status, bool IsVerified = true, string Message = VerifyEmailOtpErrors.SuccessMessage);

public static class VerifyEmailOtpErrors
{
    public const string SuccessMessage = "Xác thực email thành công. Bạn có thể đăng nhập ngay.";

    public static readonly Error InvalidOtpCode =
        new("identity.invalid_otp_code", "Mã OTP không chính xác. Vui lòng kiểm tra lại.");

    public static readonly Error OtpExpired =
        new("identity.otp_expired", "Mã OTP đã hết hạn hoặc không còn hiệu lực. Vui lòng yêu cầu mã OTP mới.");

    // The account does not exist or no longer waits for verification (already activated): same code, but the user
    // who activated earlier is pointed to the login.
    public static readonly Error AccountNotPending =
        new("identity.otp_expired", "Mã OTP không hợp lệ hoặc đã hết hạn. Nếu bạn đã kích hoạt tài khoản trước đó, vui lòng tiến hành đăng nhập.");

    public static readonly Error OtpMaxAttemptsExceeded =
        new("identity.otp_max_attempts_exceeded", $"Bạn đã nhập sai mã OTP {EmailVerification.MaxAttempts} lần nên mã này không còn dùng được. Vui lòng yêu cầu mã mới.");
}

public sealed class VerifyEmailOtpCommandValidator : AbstractValidator<VerifyEmailOtpCommand>
{
    public VerifyEmailOtpCommandValidator()
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
                return Result<VerifiedEmailAccount>.Failure(VerifyEmailOtpErrors.AccountNotPending);

            var now = clock.UtcNow;
            // Read the latest unused code even when wrong attempts invalidated it: until a new code is requested,
            // every later try keeps answering otp_max_attempts_exceeded (US-AUTH-003 spec).
            var verification = await emailVerifications.GetLatestUnusedForAccountAsync(account.Id, cancellationToken);
            if (verification is null)
                return Result<VerifiedEmailAccount>.Failure(VerifyEmailOtpErrors.OtpExpired);

            if (verification.IsExhausted)
                return Result<VerifiedEmailAccount>.Failure(VerifyEmailOtpErrors.OtpMaxAttemptsExceeded);

            if (verification.IsInvalidated)
                return Result<VerifiedEmailAccount>.Failure(AccountMessages.OtpReplaced);

            if (verification.IsExpired(now))
                return Result<VerifiedEmailAccount>.Failure(VerifyEmailOtpErrors.OtpExpired);

            var matched = verification.RecordAttempt(request.OtpCode, otpHasher, now);

            if (!matched)
            {
                await emailVerifications.SaveChangesAsync(cancellationToken);

                return verification.IsExhausted
                    ? Result<VerifiedEmailAccount>.Failure(VerifyEmailOtpErrors.OtpMaxAttemptsExceeded)
                    : Result<VerifiedEmailAccount>.Failure(AccountMessages.InvalidOtp(verification));
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
