namespace ParkingSystem.Notification.Application.Common.Identity;

public sealed record AccountContactInfo(string Email, bool IsEmailVerified);

public interface IIdentityAccountClient
{
    Task<AccountContactInfo?> GetAccountContactInfoAsync(Guid accountId, CancellationToken cancellationToken = default);
}
