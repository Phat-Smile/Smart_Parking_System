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
    string? PhoneNumber) : IRequest<Result<RegisteredAccount>>;

public sealed record RegisteredAccount(
    Guid AccountId,
    string Email,
    string? FullName,
    string Status);

public static class RegisterAccountErrors
{
    public static readonly Error EmailAlreadyRegistered =
        new("identity.email_already_registered", "The email address is already registered.");

    public static readonly Error PhoneAlreadyRegistered =
        new("identity.phone_already_registered", "The phone number is already registered to another account.");
}

public sealed partial class RegisterAccountCommandValidator : AbstractValidator<RegisterAccountCommand>
{
    public RegisterAccountCommandValidator()
    {
        // US-AUTH-001: the name is optional at registration and completed on the profile screen (US-PROFILE-001).
        RuleFor(command => command.FullName!)
            .Cascade(CascadeMode.Stop)
            .Must(name => name.Trim().Length >= 2).WithMessage("FullName must be at least 2 characters.")
            .Must(name => name.Trim().Length <= Profile.MaxFullNameLength).WithMessage($"FullName must not exceed {Profile.MaxFullNameLength} characters.")
            .Must(name => FullNameRegex().IsMatch(name.Trim())).WithMessage("FullName must only contain letters, spaces, hyphens, or apostrophes.")
            .When(command => !string.IsNullOrWhiteSpace(command.FullName));

        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(254)
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(command => command.Password).MeetsPasswordPolicy();

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
        // US-AUTH-001 (decided 2026-10-08): an existing email is rejected even while it waits for verification;
        // the client sends the user to enter or resend the OTP. No new OTP is issued here.
        if (existingAccount is not null)
            return Result<RegisteredAccount>.Failure(RegisterAccountErrors.EmailAlreadyRegistered);

        var now = clock.UtcNow;
        var account = Account.Register(
            Guid.NewGuid(),
            email,
            passwordHasher.Hash(request.Password),
            request.PhoneNumber,
            now);
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

        return Result<RegisteredAccount>.Success(new RegisteredAccount(account.Id, account.Email, profile?.FullName, account.Status.ToString()));
    }
}
