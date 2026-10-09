using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.Login;

public sealed record LoginCommand(string Email, string Password, string? DeviceInfo = null, string? IpAddress = null) : IRequest<Result<LoginResponse>>;

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string RefreshToken,
    AccountDto Account);

public sealed record AccountDto(
    Guid Id,
    string Email,
    string? PhoneNumber,
    string Status,
    string Role);

public static class LoginErrors
{
    public static readonly Error InvalidCredentials =
        new("identity.invalid_credentials", "Invalid email or password.");

    public static readonly Error EmailNotVerified =
        new("identity.email_not_verified", "Email address has not been verified. Please verify your email before logging in.");

    public static readonly Error AccountTemporarilyLocked =
        new("identity.account_temporarily_locked", "Account is temporarily locked due to multiple failed login attempts. Please try again later.");

    public static readonly Error AccountSuspended =
        new("identity.account_suspended", "Account has been suspended. Please contact support.");

    public static readonly Error AccountDisabled =
        new("identity.account_disabled", "Account has been disabled.");
}

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(254)
            .EmailAddress();

        RuleFor(command => command.Password)
            .NotEmpty()
            .Must(PasswordPolicy.FitsBcrypt).WithMessage(PasswordPolicy.TooLongMessage);
    }
}

public sealed class LoginCommandHandler(
    IAccountRepository accounts,
    IAuthSessionRepository authSessions,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IJwtTokenIssuer jwtTokenIssuer,
    IRefreshTokenGenerator refreshTokenGenerator,
    IClock clock) : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = Account.NormalizeEmail(request.Email);
        var now = clock.UtcNow;

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var account = await accounts.FindByEmailForUpdateAsync(email, cancellationToken);

            if (account is null)
            {
                passwordHasher.VerifyDummy(request.Password);
                return Result<LoginResponse>.Failure(LoginErrors.InvalidCredentials);
            }

            if (account.IsLockedOut(now))
            {
                return Result<LoginResponse>.Failure(LoginErrors.AccountTemporarilyLocked);
            }

            account.ClearExpiredLockout(now);

            var passwordMatched = passwordHasher.Verify(request.Password, account.PasswordHash);
            if (!passwordMatched)
            {
                account.RecordFailedLogin(now);
                await accounts.SaveChangesAsync(cancellationToken);

                return account.IsLockedOut(now)
                    ? Result<LoginResponse>.Failure(LoginErrors.AccountTemporarilyLocked)
                    : Result<LoginResponse>.Failure(LoginErrors.InvalidCredentials);
            }

            // Correct password breaks consecutive failed attempts streak
            account.RecordCorrectCredential();

            if (account.Status == AccountStatus.Suspended)
            {
                await accounts.SaveChangesAsync(cancellationToken);
                return Result<LoginResponse>.Failure(LoginErrors.AccountSuspended);
            }

            if (account.Status == AccountStatus.Disabled)
            {
                await accounts.SaveChangesAsync(cancellationToken);
                return Result<LoginResponse>.Failure(LoginErrors.AccountDisabled);
            }

            if (account.Status != AccountStatus.EmailVerified)
            {
                await accounts.SaveChangesAsync(cancellationToken);
                return Result<LoginResponse>.Failure(LoginErrors.EmailNotVerified);
            }

            // Create AuthSession (Token Family)
            var session = AuthSession.Create(
                Guid.NewGuid(),
                account.Id,
                request.DeviceInfo,
                request.IpAddress,
                now,
                now.AddDays(AuthSession.DefaultAbsoluteLifetimeDays));
            authSessions.Add(session);

            // Issue Refresh Token
            var (rawRefreshToken, refreshTokenHash) = refreshTokenGenerator.GenerateToken();
            var refreshToken = RefreshToken.Issue(
                Guid.NewGuid(),
                session.Id,
                account.Id,
                refreshTokenHash,
                now,
                session.CalculateNextTokenExpiration(now));
            refreshTokens.Add(refreshToken);

            // Atomic commit: updates Account state, inserts AuthSession and inserts RefreshToken in a single unit of work
            await accounts.SaveChangesAsync(cancellationToken);

            // Issue RS256 JWT Access Token
            var accessToken = jwtTokenIssuer.IssueAccessToken(account, now);

            var response = new LoginResponse(
                accessToken,
                "Bearer",
                jwtTokenIssuer.AccessTokenExpirationSeconds,
                rawRefreshToken,
                new AccountDto(account.Id, account.Email, account.PhoneNumber, account.Status.ToString(), account.Role.ToString()));

            return Result<LoginResponse>.Success(response);
        }, cancellationToken);
    }
}
