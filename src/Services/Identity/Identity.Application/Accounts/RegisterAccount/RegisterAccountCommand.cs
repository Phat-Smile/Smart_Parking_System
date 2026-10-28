using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using ParkingSystem.Contracts.Events.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Application.Profiles;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.EmailVerifications;
using ParkingSystem.Identity.Domain.Profiles;

namespace ParkingSystem.Identity.Application.Accounts.RegisterAccount;

public sealed record RegisterAccountCommand(
    string? FullName,
    string Email,
    string Password,
    string? PhoneNumber,
    string? AccountType = null) : IRequest<Result<RegisteredAccount>>;

public sealed record RegisteredAccount(
    Guid AccountId,
    string Email,
    string? FullName,
    string Status,
    string Message,
    string? OwnerApplicationStatus = null);

public static class RegisterAccountErrors
{
    public static readonly Error EmailAlreadyRegistered =
        new("identity.email_already_registered", "Email này đã được đăng ký. Vui lòng đăng nhập, hoặc dùng \"Quên mật khẩu\" nếu không nhớ mật khẩu.");

    // Same code as EmailAlreadyRegistered: only the guidance differs, the client branches on the code.
    public static readonly Error EmailPendingVerification = EmailAlreadyRegistered with
    {
        Message = "Email này đã được đăng ký nhưng chưa xác thực. Vui lòng nhập mã OTP đã gửi đến email, hoặc chọn \"Gửi lại mã\"."
    };

    public static string SuccessMessage(string email) =>
        $"Đăng ký thành công. Mã OTP đã được gửi đến {email}, có hiệu lực {EmailVerification.ExpirationMinutes} phút.";

    public static readonly Error PhoneAlreadyRegistered =
        new("identity.phone_already_registered", "Số điện thoại này đã được đăng ký cho một tài khoản khác.");
}

public sealed partial class RegisterAccountCommandValidator : AbstractValidator<RegisterAccountCommand>
{
    public RegisterAccountCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        // US-AUTH-001: Họ tên là trường tùy chọn khi đăng ký và có thể cập nhật sau trên màn hình Profile.
        RuleFor(command => command.FullName!)
            .Cascade(CascadeMode.Stop)
            .Must(name => name.Trim().Length >= 2).WithMessage("Họ và tên phải có ít nhất 2 ký tự.")
            .Must(name => name.Trim().Length <= Profile.MaxFullNameLength).WithMessage($"Họ và tên không được vượt quá {Profile.MaxFullNameLength} ký tự.")
            .Must(name => FullNameRegex().IsMatch(name.Trim())).WithMessage("Họ và tên chỉ được chứa chữ cái, khoảng trắng, dấu gạch nối hoặc dấu nháy đơn.")
            .When(command => !string.IsNullOrWhiteSpace(command.FullName));

        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email là bắt buộc, không được để trống.")
            .MaximumLength(254).WithMessage("Email không được vượt quá 254 ký tự.")
            .EmailAddress().WithMessage("Email không đúng định dạng hợp lệ.");

        RuleFor(command => command.Password).MeetsPasswordPolicy();

        // Số điện thoại là trường tùy chọn và chỉ kiểm tra khi người dùng nhập vào.
        RuleFor(command => command.PhoneNumber)
            .Matches(@"^\s*(0|\+84)[35789][0-9]{8}\s*$")
            .WithMessage("Số điện thoại phải là số di động Việt Nam: 0xxxxxxxxx (10 chữ số) hoặc +84xxxxxxxxx, chữ số sau 0/+84 là 3, 5, 7, 8 hoặc 9.")
            .When(command => !string.IsNullOrWhiteSpace(command.PhoneNumber));

        RuleFor(command => command.AccountType)
            .Must(type => type is null
                || string.Equals(type.Trim(), "Customer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type.Trim(), "ParkingLotOwner", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Loại tài khoản không hợp lệ, chỉ chấp nhận Customer hoặc ParkingLotOwner.");
    }

    [GeneratedRegex(@"^[\p{L}]+(?:[ '-][\p{L}]+)*$")]
    private static partial Regex FullNameRegex();
}

public sealed class RegisterAccountCommandHandler(
    IAccountRepository accounts,
    IProfileRepository profiles,
    IEmailVerificationRepository emailVerifications,
    IPasswordHasher passwordHasher,
    IOtpGenerator otpGenerator,
    IOtpHasher otpHasher,
    IOutboxWriter outboxWriter,
    IClock clock) : IRequestHandler<RegisterAccountCommand, Result<RegisteredAccount>>
{
    public async Task<Result<RegisteredAccount>> Handle(RegisterAccountCommand request, CancellationToken cancellationToken)
    {
        var email = Account.NormalizeEmail(request.Email);
        var existingAccount = await accounts.FindByEmailAsync(email, cancellationToken);
        // US-AUTH-001 (decided 2026-10-08): an existing email is rejected even while it waits for verification;
        // the client sends the user to enter or resend the OTP. No new OTP is issued here.
        if (existingAccount is not null)
            return Result<RegisteredAccount>.Failure(existingAccount.Status == AccountStatus.PendingEmailVerification
                ? RegisterAccountErrors.EmailPendingVerification
                : RegisterAccountErrors.EmailAlreadyRegistered);

        var now = clock.UtcNow;
        var isOwner = string.Equals(request.AccountType?.Trim(), "ParkingLotOwner", StringComparison.OrdinalIgnoreCase);
        var account = Account.Register(
            Guid.NewGuid(),
            email,
            passwordHasher.Hash(request.Password),
            request.PhoneNumber,
            now,
            applyAsParkingLotOwner: isOwner);
        accounts.Add(account);

        // Without a name the profile is created later on the profile screen; never store a placeholder name.
        Profile? profile = null;
        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            profile = Profile.Create(account.Id, request.FullName, now);
            profiles.Add(profile);
        }

        // US-AUTH-001 & US-AUTH-003: Issue initial email OTP verification and outbox message in same transaction
        var otpCode = otpGenerator.GenerateNumericOtp(EmailVerification.OtpLength);
        var otpHash = otpHasher.HashOtp(otpCode, account.Id);
        var verification = EmailVerification.Issue(Guid.NewGuid(), account.Id, email, otpHash, now);
        emailVerifications.Add(verification);

        outboxWriter.Enqueue(
            new UserVerificationOtpIssuedIntegrationEvent(account.Id, email, otpCode, verification.ExpiresAtUtc),
            "identity.user-verification-otp-issued.v1",
            1);

        // A concurrent registration with the same email loses the race on the unique index.
        var saveResult = await accounts.SaveNewAccountAsync(cancellationToken);
        if (saveResult == SaveAccountResult.DuplicatePhoneConflict)
            return Result<RegisteredAccount>.Failure(RegisterAccountErrors.PhoneAlreadyRegistered);

        if (saveResult == SaveAccountResult.DuplicateEmailConflict)
            return Result<RegisteredAccount>.Failure(RegisterAccountErrors.EmailAlreadyRegistered);

        return Result<RegisteredAccount>.Success(new RegisteredAccount(
            account.Id,
            account.Email,
            profile?.FullName,
            account.Status.ToString(),
            RegisterAccountErrors.SuccessMessage(account.Email),
            account.OwnerApplicationStatus?.ToString()));
    }
}
