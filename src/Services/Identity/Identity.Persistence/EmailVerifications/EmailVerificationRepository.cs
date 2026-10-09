using Microsoft.EntityFrameworkCore;
using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Domain.EmailVerifications;

namespace ParkingSystem.Identity.Persistence.EmailVerifications;

internal sealed class EmailVerificationRepository(IdentityDbContext dbContext) : IEmailVerificationRepository
{
    public Task<EmailVerification?> GetLatestActiveForAccountAsync(Guid accountId, CancellationToken cancellationToken, OtpPurpose purpose = OtpPurpose.EmailVerification) =>
        dbContext.EmailVerifications
            .Where(verification => verification.AccountId == accountId && verification.Purpose == purpose && !verification.IsUsed && verification.InvalidatedAtUtc == null)
            .OrderByDescending(verification => verification.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<EmailVerification?> GetLatestUnusedForAccountAsync(Guid accountId, CancellationToken cancellationToken, OtpPurpose purpose = OtpPurpose.EmailVerification) =>
        dbContext.EmailVerifications
            .Where(verification => verification.AccountId == accountId && verification.Purpose == purpose && !verification.IsUsed)
            .OrderByDescending(verification => verification.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<DateTimeOffset?> GetOldestIssuedSinceAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken, OtpPurpose purpose = OtpPurpose.EmailVerification) =>
        dbContext.EmailVerifications
            .AsNoTracking()
            .Where(verification => verification.Email == normalizedEmail && verification.Purpose == purpose && verification.CreatedAtUtc >= sinceUtc)
            .OrderBy(verification => verification.CreatedAtUtc)
            .Select(verification => (DateTimeOffset?)verification.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> CountRecentResendsInPastHourAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken, OtpPurpose purpose = OtpPurpose.EmailVerification) =>
        dbContext.EmailVerifications
            .AsNoTracking()
            .CountAsync(
                verification => verification.Email == normalizedEmail && verification.Purpose == purpose && verification.CreatedAtUtc >= sinceUtc,
                cancellationToken);

    public Task<EmailVerification?> GetLatestResendInCooldownAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken, OtpPurpose purpose = OtpPurpose.EmailVerification) =>
        dbContext.EmailVerifications
            .AsNoTracking()
            .Where(verification => verification.Email == normalizedEmail && verification.Purpose == purpose && verification.CreatedAtUtc >= sinceUtc)
            .OrderByDescending(verification => verification.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(EmailVerification emailVerification) =>
        dbContext.EmailVerifications.Add(emailVerification);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
