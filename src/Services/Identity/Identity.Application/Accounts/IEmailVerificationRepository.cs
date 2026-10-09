using ParkingSystem.Identity.Domain.EmailVerifications;

namespace ParkingSystem.Identity.Application.Accounts;

public interface IEmailVerificationRepository
{
    // Every query is scoped to one OTP purpose (EmailVerification by default) so the flows never share codes or quota.
    Task<EmailVerification?> GetLatestActiveForAccountAsync(Guid accountId, CancellationToken cancellationToken, OtpPurpose purpose = OtpPurpose.EmailVerification);

    // Latest code not used yet, including one invalidated by too many wrong attempts.
    Task<EmailVerification?> GetLatestUnusedForAccountAsync(Guid accountId, CancellationToken cancellationToken, OtpPurpose purpose = OtpPurpose.EmailVerification);

    Task<DateTimeOffset?> GetOldestIssuedSinceAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken, OtpPurpose purpose = OtpPurpose.EmailVerification);

    Task<int> CountRecentResendsInPastHourAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken, OtpPurpose purpose = OtpPurpose.EmailVerification);

    Task<EmailVerification?> GetLatestResendInCooldownAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken, OtpPurpose purpose = OtpPurpose.EmailVerification);

    void Add(EmailVerification emailVerification);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
