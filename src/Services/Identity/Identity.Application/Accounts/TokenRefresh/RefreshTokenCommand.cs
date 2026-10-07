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
    string? IpAddress = null) : IRequest<Result<LoginResponse>>;

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
}

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(command => command.RefreshToken)
            .NotEmpty().WithMessage("RefreshToken is required.");
    }
}

public sealed class RefreshTokenCommandHandler(
    IAccountRepository accounts,
    IAuthSessionRepository authSessions,
    IRefreshTokenRepository refreshTokens,
    IJwtTokenIssuer jwtTokenIssuer,
    IRefreshTokenGenerator refreshTokenGenerator,
    IClock clock) : IRequestHandler<RefreshTokenCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var tokenHash = refreshTokenGenerator.HashToken(request.RefreshToken);

        return await accounts.ExecuteInTransactionAsync(async () =>
        {
            var token = await refreshTokens.FindByHashAsync(tokenHash, cancellationToken);
            if (token is null)
            {
                return Result<LoginResponse>.Failure(RefreshTokenErrors.InvalidRefreshToken);
            }

            // Row-level lock on AuthSession to eliminate race conditions
            var session = await authSessions.FindByIdForUpdateAsync(token.SessionId, cancellationToken);
            if (session is null)
            {
                return Result<LoginResponse>.Failure(RefreshTokenErrors.InvalidRefreshToken);
            }

            if (session.IsRevoked)
            {
                return Result<LoginResponse>.Failure(RefreshTokenErrors.SessionRevoked);
            }

            if (session.IsExpired(now))
            {
                return Result<LoginResponse>.Failure(RefreshTokenErrors.SessionExpired);
            }

            // Check if token was already revoked
            if (token.IsRevoked)
            {
                // Grace window check (<= 10 seconds after rotation) to tolerate concurrent multi-tab requests
                if (token.RevokedAtUtc.HasValue &&
                    now - token.RevokedAtUtc.Value <= TimeSpan.FromSeconds(10) &&
                    token.ReplacedByTokenId.HasValue)
                {
                    var latestToken = await refreshTokens.FindLatestBySessionIdAsync(session.Id, cancellationToken);
                    if (latestToken is not null && latestToken.IsActive(now))
                    {
                        var accountForGrace = await accounts.FindByIdAsync(session.AccountId, cancellationToken);
                        if (accountForGrace is not null &&
                            accountForGrace.Status != AccountStatus.Suspended &&
                            accountForGrace.Status != AccountStatus.Disabled)
                        {
                            var graceAccessToken = jwtTokenIssuer.IssueAccessToken(accountForGrace, now);
                            return Result<LoginResponse>.Success(new LoginResponse(
                                graceAccessToken,
                                "Bearer",
                                jwtTokenIssuer.AccessTokenExpirationSeconds,
                                string.Empty,
                                new AccountDto(accountForGrace.Id, accountForGrace.Email, accountForGrace.PhoneNumber, accountForGrace.Status.ToString(), accountForGrace.Role.ToString())));
                        }
                    }
                }

                // TOKEN REUSE DETECTED: Scoped revocation to this compromised session only
                session.Revoke(now, "ReuseDetected");
                await refreshTokens.RevokeActiveTokensBySessionIdAsync(session.Id, now, "FamilyCompromised", cancellationToken);

                // Commit side-effect to database before returning 401
                await accounts.SaveChangesAsync(cancellationToken);

                return Result<LoginResponse>.Failure(RefreshTokenErrors.TokenReuseDetected);
            }

            if (token.IsExpired(now))
            {
                return Result<LoginResponse>.Failure(RefreshTokenErrors.RefreshTokenExpired);
            }

            var account = await accounts.FindByIdAsync(session.AccountId, cancellationToken);
            if (account is null)
            {
                return Result<LoginResponse>.Failure(RefreshTokenErrors.InvalidRefreshToken);
            }

            if (account.Status == AccountStatus.Suspended)
            {
                return Result<LoginResponse>.Failure(RefreshTokenErrors.AccountSuspended);
            }

            if (account.Status == AccountStatus.Disabled)
            {
                return Result<LoginResponse>.Failure(RefreshTokenErrors.AccountDisabled);
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
            var response = new LoginResponse(
                accessToken,
                "Bearer",
                jwtTokenIssuer.AccessTokenExpirationSeconds,
                newRawRefreshToken,
                new AccountDto(account.Id, account.Email, account.PhoneNumber, account.Status.ToString(), account.Role.ToString()));

            return Result<LoginResponse>.Success(response);
        }, cancellationToken);
    }
}
