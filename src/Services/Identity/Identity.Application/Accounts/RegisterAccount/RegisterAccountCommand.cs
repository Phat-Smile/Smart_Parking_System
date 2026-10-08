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
    string FullName,
    string Email,
    string Password,
    string? PhoneNumber) : IRequest<Result<RegisteredAccount>>;

public sealed record RegisteredAccount(
    Guid AccountId,
    string Email,
    string FullName,
    string Status,
    bool IsNewAccount = true);

public static class RegisterAccountErrors
{
    public static readonly Error EmailAlreadyRegistered =
        new("identity.email_already_registered", "The email address is already registered.");
}

public sealed partial class RegisterAccountCommandValidator : AbstractValidator<RegisterAccountCommand>
{
    public RegisterAccountCommandValidator()
    {
        RuleFor(command => command.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("FullName is required.")
            .Must(name => name.Trim().Length >= 2).WithMessage("FullName must be at least 2 characters.")
            .Must(name => name.Trim().Length <= Profile.MaxFullNameLength).WithMessage($"FullName must not exceed {Profile.MaxFullNameLength} characters.")
            .Must(name => FullNameRegex().IsMatch(name.Trim())).WithMessage("FullName must only contain letters, spaces, hyphens, or apostrophes.");

        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(254)
            .EmailAddress().WithMessage("Email must be a valid email address.");

        // Context 6.1: at least 8 characters with upper case, lower case, digit and special character.
        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8)
            .MaximumLength(72) // bcrypt only uses the first 72 bytes.
            .Matches("[A-Z]").WithMessage("Password must contain an upper-case letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lower-case letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .Matches("[^A-Za-z0-9]").WithMessage("Password must contain a special character.");

        // Phone number is optional and only validated when supplied. Vietnamese mobile numbers
        // start with 3, 5, 7, 8 or 9 after the '0' or '+84' prefix; the account stores the +84 form.
        RuleFor(command => command.PhoneNumber)
            .Matches(@"^\s*(0|\+84)[35789][0-9]{8}\s*$")
            .WithMessage("Phone number must be a Vietnamese mobile number: 0 or +84 followed by 9 digits starting with 3, 5, 7, 8 or 9.")
            .When(command => !string.IsNullOrWhiteSpace(command.PhoneNumber));
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
        if (existingAccount is not null)
        {
            // Edge-case UX recovery: If account was registered previously but remains in PendingEmailVerification,
            // return success with the pending account information so the client can proceed to OTP verification/resend.
            if (existingAccount.Status == AccountStatus.PendingEmailVerification)
                return PendingAccountRecovered(existingAccount, request);

            return Result<RegisteredAccount>.Failure(RegisterAccountErrors.EmailAlreadyRegistered);
        }

        var now = clock.UtcNow;
        var account = Account.Register(
            Guid.NewGuid(),
            email,
            passwordHasher.Hash(request.Password),
            request.PhoneNumber,
            now);
        accounts.Add(account);

        var profile = Profile.Create(
            account.Id,
            request.FullName,
            now);
        profiles.Add(profile);

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
        if (saveResult == SaveAccountResult.DuplicateEmailConflict)
        {
            // Re-query to determine authoritative business state (Dormant Recovery vs Email Already Registered)
            var concurrentAccount = await accounts.FindByEmailAsync(email, cancellationToken);
            if (concurrentAccount is not null && concurrentAccount.Status == AccountStatus.PendingEmailVerification)
                return PendingAccountRecovered(concurrentAccount, request);

            return Result<RegisteredAccount>.Failure(RegisterAccountErrors.EmailAlreadyRegistered);
        }

        return Result<RegisteredAccount>.Success(new RegisteredAccount(account.Id, account.Email, profile.FullName, account.Status.ToString()));
    }

    // No password check happens here, so echo the caller's own name: knowing an email
    // must not reveal the name stored by whoever registered it first.
    private static Result<RegisteredAccount> PendingAccountRecovered(Account pendingAccount, RegisterAccountCommand request) =>
        Result<RegisteredAccount>.Success(new RegisteredAccount(
            pendingAccount.Id,
            pendingAccount.Email,
            request.FullName.Trim(),
            pendingAccount.Status.ToString(),
            IsNewAccount: false));
}
