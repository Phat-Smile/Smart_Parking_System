using ParkingSystem.Identity.Domain.Mfa;

namespace ParkingSystem.Identity.Application.Accounts.Mfa;

// Lưu chung DbContext với Account: thay đổi được commit bằng IAccountRepository.SaveChangesAsync trong cùng giao dịch.
public interface IMfaRepository
{
    Task<AccountMfa?> FindAsync(Guid accountId, CancellationToken cancellationToken);

    Task<bool> IsEnabledAsync(Guid accountId, CancellationToken cancellationToken);

    void Add(AccountMfa mfa);

    void Remove(AccountMfa mfa);

    Task<IReadOnlyList<MfaRecoveryCode>> ListUnusedRecoveryCodesAsync(Guid accountId, CancellationToken cancellationToken);

    void AddRecoveryCodes(IEnumerable<MfaRecoveryCode> codes);

    Task DeleteRecoveryCodesAsync(Guid accountId, CancellationToken cancellationToken);

    void AddChallenge(MfaChallenge challenge);

    Task<MfaChallenge?> FindChallengeByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    // Đọc lại sau khi khoá dòng account để thấy kết quả của request song song cùng challenge.
    Task ReloadAsync(MfaChallenge challenge, CancellationToken cancellationToken);
}
