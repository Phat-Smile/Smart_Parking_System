using ParkingSystem.Identity.Application.Accounts;
using ParkingSystem.Identity.Domain.EmailVerifications;

namespace ParkingSystem.Tests.Identity.UnitTests;

// Shared in-memory IEmailVerificationRepository: every query is scoped to one OTP purpose, like the EF repository.
internal sealed class InMemoryEmailVerificationRepository(IEnumerable<EmailVerification>? initial = null) : IEmailVerificationRepository
{
    public List<EmailVerification> Saved { get; } = [.. initial ?? []];

    public Task<EmailVerification?> GetLatestActiveForAccountAsync(Guid accountId, CancellationToken cancellationToken, OtpPurpose purpose = OtpPurpose.EmailVerification) =>
        Task.FromResult(Saved
            .Where(v => v.AccountId == accountId && v.Purpose == purpose && !v.IsUsed && v.InvalidatedAtUtc == null)
            .OrderByDescending(v => v.CreatedAtUtc)
            .FirstOrDefault());

    public Task<EmailVerification?> GetLatestUnusedForAccountAsync(Guid accountId, CancellationToken cancellationToken, OtpPurpose purpose = OtpPurpose.EmailVerification) =>
        Task.FromResult(Saved
            .Where(v => v.AccountId == accountId && v.Purpose == purpose)
            .OrderByDescending(v => v.CreatedAtUtc)
            .FirstOrDefault() is { IsUsed: false } latest ? latest : null);

    public Task<DateTimeOffset?> GetOldestIssuedSinceAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken, OtpPurpose purpose = OtpPurpose.EmailVerification) =>
        Task.FromResult(Saved
            .Where(v => v.Email == normalizedEmail && v.Purpose == purpose && v.CreatedAtUtc >= sinceUtc)
            .OrderBy(v => v.CreatedAtUtc)
            .Select(v => (DateTimeOffset?)v.CreatedAtUtc)
            .FirstOrDefault());

    public Task<int> CountRecentResendsInPastHourAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken, OtpPurpose purpose = OtpPurpose.EmailVerification) =>
        Task.FromResult(Saved.Count(v => v.Email == normalizedEmail && v.Purpose == purpose && v.CreatedAtUtc >= sinceUtc));

    public Task<EmailVerification?> GetLatestResendInCooldownAsync(string normalizedEmail, DateTimeOffset sinceUtc, CancellationToken cancellationToken, OtpPurpose purpose = OtpPurpose.EmailVerification) =>
        Task.FromResult(Saved
            .Where(v => v.Email == normalizedEmail && v.Purpose == purpose && v.CreatedAtUtc >= sinceUtc)
            .OrderByDescending(v => v.CreatedAtUtc)
            .FirstOrDefault());

    public void Add(EmailVerification emailVerification) => Saved.Add(emailVerification);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
