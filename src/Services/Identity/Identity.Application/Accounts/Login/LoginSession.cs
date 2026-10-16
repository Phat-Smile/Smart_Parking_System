using ParkingSystem.Identity.Application.Common.Security;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.Login;

// Shared by password and Google login: one new session (token family) with its first refresh token and an access token.
internal static class LoginSession
{
    public static async Task<LoginResponse> StartAsync(
        Account account,
        string? deviceInfo,
        string? ipAddress,
        DateTimeOffset now,
        IAccountRepository accounts,
        IAuthSessionRepository authSessions,
        IRefreshTokenRepository refreshTokens,
        IRefreshTokenGenerator refreshTokenGenerator,
        IJwtTokenIssuer jwtTokenIssuer,
        CancellationToken cancellationToken)
    {
        var session = AuthSession.Create(
            Guid.NewGuid(),
            account.Id,
            deviceInfo,
            ipAddress,
            now,
            now.AddDays(AuthSession.DefaultAbsoluteLifetimeDays));
        authSessions.Add(session);

        var (rawRefreshToken, refreshTokenHash) = refreshTokenGenerator.GenerateToken();
        var refreshTokenExpiresAt = session.CalculateNextTokenExpiration(now);
        refreshTokens.Add(RefreshToken.Issue(
            Guid.NewGuid(),
            session.Id,
            account.Id,
            refreshTokenHash,
            now,
            refreshTokenExpiresAt));

        // Account state, session and refresh token are committed together.
        await accounts.SaveChangesAsync(cancellationToken);

        return new LoginResponse(
            jwtTokenIssuer.IssueAccessToken(account, session.Id, now),
            "Bearer",
            jwtTokenIssuer.AccessTokenExpirationSeconds,
            rawRefreshToken,
            new AccountDto(account.Id, account.Email, account.PhoneNumber, account.Status.ToString(), account.Role.ToString()),
            refreshTokenExpiresAt);
    }
}
