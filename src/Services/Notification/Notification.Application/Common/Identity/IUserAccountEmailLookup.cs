namespace ParkingSystem.Notification.Application.Common.Identity;

/// <summary>
/// Looks up account email and verification status from Identity service before executing email notifications.
/// </summary>
public sealed record UserAccountEmailInfo(Guid AccountId, string Email, bool IsEmailVerified);

public interface IUserAccountEmailLookup
{
    Task<UserAccountEmailInfo?> LookupAccountEmailAsync(Guid accountId, CancellationToken cancellationToken);
}
