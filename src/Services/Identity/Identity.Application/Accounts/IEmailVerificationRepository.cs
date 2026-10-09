using ParkingSystem.Identity.Domain.EmailVerifications;

namespace ParkingSystem.Identity.Application.Accounts;

public interface IEmailVerificationRepository
{
    Task<EmailVerification?> GetLatestActiveForAccountAsync(Guid accountId, CancellationToken cancellationToken);

    // Latest code not used yet, including one invalidated by too many wrong attempts.
    Task<EmailVerification?> GetLatestUnusedForAccountAsync(Guid accountId, CancellationToken cancellationToken);

    Task<DateTimeOffset?> GetOldestIssuedSinceAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken);

    Task<int> CountRecentResendsInPastHourAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken);

    Task<EmailVerification?> GetLatestResendInCooldownAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken);

    void Add(EmailVerification emailVerification);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
