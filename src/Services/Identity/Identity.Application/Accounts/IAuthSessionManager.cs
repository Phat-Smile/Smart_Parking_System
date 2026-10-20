namespace ParkingSystem.Identity.Application.Accounts;

public interface IAuthSessionManager
{
    Task RevokeSessionAsync(Guid sessionId, string reason, CancellationToken cancellationToken = default);

    Task RevokeAllSessionsAsync(Guid accountId, string reason, CancellationToken cancellationToken = default);
}
