using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.BuildingBlocks.Events;
using ParkingSystem.Identity.Application.Common.Messaging.Outbox;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.EmailVerifications;

namespace ParkingSystem.Identity.Application.Accounts.RegisterAccount;

public sealed record RegisterAccountCommand(string Email, string Password, string? PhoneNumber)
    : IRequest<Result<RegisteredAccount>>;

public sealed record RegisteredAccount(Guid AccountId, string Email, string Status);

public static class RegisterAccountErrors
{
    public static readonly Error EmailAlreadyRegistered =
        new("identity.email_already_registered", "The email address is already registered.");
}

public sealed class RegisterAccountCommandValidator : AbstractValidator<RegisterAccountCommand>
{
    public RegisterAccountCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(254).EmailAddress();

        // Context 6.1: at least 8 characters with upper case, lower case, digit and special character.
        RuleFor(command => command.Password)
            .NotEmpty()
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
}

public sealed class RegisterAccountCommandHandler(
    IAccountRepository accounts,
    IEmailVerificationRepository emailVerifications,
    IPasswordHasher passwordHasher,
    IOtpGenerator otpGenerator,
    IOutboxWriter outboxWriter,
    IClock clock) : IRequestHandler<RegisterAccountCommand, Result<RegisteredAccount>>
{
    public async Task<Result<RegisteredAccount>> Handle(RegisterAccountCommand request, CancellationToken cancellationToken)
    {
        var email = Account.NormalizeEmail(request.Email);
        if (await accounts.EmailExistsAsync(email, cancellationToken))
            return Result<RegisteredAccount>.Failure(RegisterAccountErrors.EmailAlreadyRegistered);

        var now = clock.UtcNow;
        var account = Account.Register(
            Guid.NewGuid(),
            email,
            passwordHasher.Hash(request.Password),
            request.PhoneNumber,
            now);
        accounts.Add(account);

        // US-AUTH-001 & US-AUTH-003: Issue initial email OTP verification and outbox message in same transaction
        var otpCode = otpGenerator.GenerateNumericOtp(EmailVerification.OtpLength);
        var verification = EmailVerification.Issue(Guid.NewGuid(), account.Id, email, otpCode, now);
        emailVerifications.Add(verification);

        outboxWriter.Enqueue(
            new UserVerificationOtpIssuedIntegrationEvent(account.Id, email, otpCode, verification.ExpiresAtUtc),
            "identity.user-verification-otp-issued.v1",
            1);

        // A concurrent registration with the same email loses the race on the unique index.
        if (!await accounts.TrySaveChangesAsync(cancellationToken))
            return Result<RegisteredAccount>.Failure(RegisterAccountErrors.EmailAlreadyRegistered);

        return Result<RegisteredAccount>.Success(new RegisteredAccount(account.Id, account.Email, account.Status.ToString()));
    }
}
