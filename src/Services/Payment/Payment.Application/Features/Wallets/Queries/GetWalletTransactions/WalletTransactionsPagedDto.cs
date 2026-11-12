namespace ParkingSystem.Payment.Application.Features.Wallets.Queries.GetWalletTransactions;

public record WalletTransactionItemDto(
    Guid EntryId,
    DateTimeOffset CreatedAtUtc,
    string EntryType,
    long Amount,
    long BalanceDelta,
    bool AffectsBalance,
    long BalanceAfter,
    string? Reason,
    string? ReferenceType,
    string? ReferenceId
);

public record WalletTransactionsPagedDto(
    IReadOnlyList<WalletTransactionItemDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages
);
