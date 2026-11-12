using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Common.Security;

public interface IJwtTokenIssuer
{
    int AccessTokenExpirationSeconds { get; }

    string IssueAccessToken(Account account, DateTimeOffset nowUtc);

    // sessionId thành claim "sid": cho biết token thuộc phiên nào.
    // authenticationMethods (vd "pwd mfa") thành claim mảng "amr"; API quản trị yêu cầu có "mfa".
    string IssueAccessToken(Account account, Guid sessionId, string authenticationMethods, DateTimeOffset nowUtc);
}
