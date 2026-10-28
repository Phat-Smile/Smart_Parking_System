using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Application.Common.Interfaces;

public record WalletTransactionFilter(
    LedgerEntryType? Type,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int Page,
    int PageSize
);

public record WalletTransactionRawItem(
    Guid Id,
    DateTimeOffset CreatedAt,
    LedgerEntryType EntryType,
    decimal Amount,
    decimal BalanceAfter,
    string? Reason,
    ReferenceType? ReferenceType,
    string? ReferenceId,
    long Sequence
);

public interface IWalletTransactionQueryRepository
{
    // Đọc lịch sử giao dịch bằng projection không tracking
    Task<(IReadOnlyList<WalletTransactionRawItem> Items, int TotalCount)> GetTransactionsAsync(
        Guid walletId,
        WalletTransactionFilter filter,
        CancellationToken cancellationToken);
}
