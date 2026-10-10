using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Common.Security;

public interface IJwtTokenIssuer
{
    int AccessTokenExpirationSeconds { get; }

    // sessionId thành claim "sid": cho biết token thuộc phiên nào (màn thiết bị đang đăng nhập đánh dấu phiên hiện tại).
    string IssueAccessToken(Account account, Guid sessionId, DateTimeOffset nowUtc);
}
