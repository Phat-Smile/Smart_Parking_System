using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Common.Security;

public interface IJwtTokenIssuer
{
    int AccessTokenExpirationSeconds { get; }

    string IssueAccessToken(Account account, DateTimeOffset nowUtc);
}
