using Microsoft.EntityFrameworkCore;
using ParkingSystem.Identity.Application.Accounts.Mfa;
using ParkingSystem.Identity.Domain.Mfa;

namespace ParkingSystem.Identity.Persistence.Mfa;

internal sealed class MfaRepository(IdentityDbContext dbContext) : IMfaRepository
{
    public Task<AccountMfa?> FindAsync(Guid accountId, CancellationToken cancellationToken) =>
        dbContext.AccountMfas.SingleOrDefaultAsync(m => m.AccountId == accountId, cancellationToken);

    public Task<bool> IsEnabledAsync(Guid accountId, CancellationToken cancellationToken) =>
        dbContext.AccountMfas.AsNoTracking().AnyAsync(m => m.AccountId == accountId && m.EnabledAtUtc != null, cancellationToken);

    public void Add(AccountMfa mfa) => dbContext.AccountMfas.Add(mfa);

    public void Remove(AccountMfa mfa) => dbContext.AccountMfas.Remove(mfa);

    public async Task<IReadOnlyList<MfaRecoveryCode>> ListUnusedRecoveryCodesAsync(Guid accountId, CancellationToken cancellationToken) =>
        await dbContext.MfaRecoveryCodes.Where(c => c.AccountId == accountId && c.UsedAtUtc == null).ToListAsync(cancellationToken);

    public void AddRecoveryCodes(IEnumerable<MfaRecoveryCode> codes) => dbContext.MfaRecoveryCodes.AddRange(codes);

    // Xoá thẳng trong DB (chạy trong giao dịch đang mở của handler), trước khi thêm bộ mã mới.
    public Task DeleteRecoveryCodesAsync(Guid accountId, CancellationToken cancellationToken) =>
        dbContext.MfaRecoveryCodes.Where(c => c.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);

    public void AddChallenge(MfaChallenge challenge) => dbContext.MfaChallenges.Add(challenge);

    public Task<MfaChallenge?> FindChallengeByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.MfaChallenges.SingleOrDefaultAsync(c => c.TokenHash == tokenHash, cancellationToken);

    public Task ReloadAsync(MfaChallenge challenge, CancellationToken cancellationToken) =>
        dbContext.Entry(challenge).ReloadAsync(cancellationToken);
}
