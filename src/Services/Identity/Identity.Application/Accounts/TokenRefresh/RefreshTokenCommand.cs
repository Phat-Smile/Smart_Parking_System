using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Identity.Application.Accounts.Login;
using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.TokenRefresh;

public sealed record RefreshTokenCommand(
    string RefreshToken,
    string? DeviceInfo = null,
    string? IpAddress = null) : IRequest<Result<RefreshTokenResponse>>;

public sealed record RefreshTokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string RefreshToken,
    AccountDto Account);

public static class RefreshTokenErrors
{
    public static readonly Error InvalidRefreshToken =
        new("identity.invalid_refresh_token", "The provided refresh token is invalid.");

    public static readonly Error RefreshTokenExpired =
        new("identity.refresh_token_expired", "The refresh token has expired. Please login again.");

    public static readonly Error SessionExpired =
        new("identity.session_expired", "The session has reached its maximum absolute lifetime. Please login again.");

    public static readonly Error SessionRevoked =
        new("identity.session_revoked", "The session has been revoked. Please login again.");

    public static readonly Error TokenReuseDetected =
        new("identity.refresh_token_reused", "Token reuse detected. The current session has been terminated for security.");

    public static readonly Error AccountSuspended =
        new("identity.account_suspended", "Account is suspended. Please contact support.");

    public static readonly Error AccountDisabled =
        new("identity.account_disabled", "Account is disabled.");

    public static readonly Error EmailNotVerified =
        new("identity.email_not_verified", "Email is not verified.");
}

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(command => command.RefreshToken)
            .NotEmpty().WithMessage("RefreshToken is required.")
            .MaximumLength(512).WithMessage("RefreshToken must not exceed 512 characters.");
    }
}

public sealed class RefreshTokenCommandHandler(
    IAccountRepository accounts,
    IAuthSessionRepository authSessions,
    IRefreshTokenRepository refreshTokens,
    IJwtTokenIssuer jwtTokenIssuer,
    IRefreshTokenGenerator refreshTokenGenerator,
    IClock clock) : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>
{
    public async Task<Result<RefreshTokenResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var tokenHash = refreshTokenGenerator.HashToken(request.RefreshToken);

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var token = await refreshTokens.FindByHashAsync(tokenHash, cancellationToken);
            if (token is null)
            {
                return Result<RefreshTokenResponse>.Failure(RefreshTokenErrors.InvalidRefreshToken);
            }

            // Row-level lock on AuthSession to eliminate race conditions
            var session = await authSessions.FindByIdForUpdateAsync(token.SessionId, cancellationToken);
            if (session is null)
            {
                return Result<RefreshTokenResponse>.Failure(RefreshTokenErrors.InvalidRefreshToken);
            }

            // Re-read/reload token state in case a concurrent request rotated/revoked it while we were waiting for the lock
            await refreshTokens.ReloadAsync(token, cancellationToken);

            if (session.IsRevoked)
            {
                return Result<RefreshTokenResponse>.Failure(RefreshTokenErrors.SessionRevoked);
            }

            if (session.IsExpired(now))
            {
                return Result<RefreshTokenResponse>.Failure(RefreshTokenErrors.SessionExpired);
            }

            // Strict Single-Use Rotation Check: any presentation of an already revoked token is a reuse attack
            if (token.IsRevoked)
            {
                // TOKEN REUSE DETECTED: Scoped revocation to this compromised session only
                session.Revoke(now, "ReuseDetected");
                await refreshTokens.RevokeActiveTokensBySessionIdAsync(session.Id, now, "FamilyCompromised", cancellationToken);

                // Commit side-effect to database before returning 401
                await accounts.SaveChangesAsync(cancellationToken);

                return Result<RefreshTokenResponse>.Failure(RefreshTokenErrors.TokenReuseDetected);
            }

            if (token.IsExpired(now))
            {
                return Result<RefreshTokenResponse>.Failure(RefreshTokenErrors.RefreshTokenExpired);
            }

            var account = await accounts.FindByIdAsync(session.AccountId, cancellationToken);
            if (account is null)
            {
                return Result<RefreshTokenResponse>.Failure(RefreshTokenErrors.InvalidRefreshToken);
            }

            if (account.Status == AccountStatus.Suspended)
            {
                session.Revoke(now, "AccountSuspended");
                await refreshTokens.RevokeActiveTokensBySessionIdAsync(session.Id, now, "AccountSuspended", cancellationToken);
                await accounts.SaveChangesAsync(cancellationToken);
                return Result<RefreshTokenResponse>.Failure(RefreshTokenErrors.AccountSuspended);
            }

            if (account.Status == AccountStatus.Disabled)
            {
                session.Revoke(now, "AccountDisabled");
                await refreshTokens.RevokeActiveTokensBySessionIdAsync(session.Id, now, "AccountDisabled", cancellationToken);
                await accounts.SaveChangesAsync(cancellationToken);
                return Result<RefreshTokenResponse>.Failure(RefreshTokenErrors.AccountDisabled);
            }

            if (account.Status != AccountStatus.EmailVerified)
            {
                session.Revoke(now, "EmailNotVerified");
                await refreshTokens.RevokeActiveTokensBySessionIdAsync(session.Id, now, "EmailNotVerified", cancellationToken);
                await accounts.SaveChangesAsync(cancellationToken);
                return Result<RefreshTokenResponse>.Failure(RefreshTokenErrors.EmailNotVerified);
            }

            // NOTE: Temporary password lockout (3 failed password attempts) does NOT block active sessions

            // ROTATE REFRESH TOKEN
            var (newRawRefreshToken, newRefreshTokenHash) = refreshTokenGenerator.GenerateToken();
            var newRefreshToken = RefreshToken.Issue(
                Guid.NewGuid(),
                session.Id,
                account.Id,
                newRefreshTokenHash,
                now,
                session.CalculateNextTokenExpiration(now));

            token.Revoke(now, "Rotated", newRefreshToken.Id, newRefreshTokenHash);
            session.RecordActivity(now);
            refreshTokens.Add(newRefreshToken);

            await accounts.SaveChangesAsync(cancellationToken);

            var accessToken = jwtTokenIssuer.IssueAccessToken(account, now);
            var response = new RefreshTokenResponse(
                accessToken,
                "Bearer",
                jwtTokenIssuer.AccessTokenExpirationSeconds,
                newRawRefreshToken,
                new AccountDto(account.Id, account.Email, account.PhoneNumber, account.Status.ToString(), account.Role.ToString()));

            return Result<RefreshTokenResponse>.Success(response);
        }, cancellationToken);
    }
}
