using System;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Domain.Entities;

public class WalletLedgerEntry
{
    public Guid Id { get; private set; }
    public Guid WalletId { get; private set; }
    public long Sequence { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public LedgerEntryType EntryType { get; private set; }
    public decimal Amount { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public string? Reason { get; private set; }
    public ReferenceType? ReferenceType { get; private set; }
    public string? ReferenceId { get; private set; }
    public Guid? LotId { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public Guid? ReversalOfEntryId { get; private set; }
    public Guid? CreatedBy { get; private set; }

    private WalletLedgerEntry() { }

    // Ép buộc chỉ class Wallet mới được quyền in Biên lai
    internal WalletLedgerEntry(
        Guid walletId,
        long sequence,
        LedgerEntryType entryType,
        decimal amount,
        decimal balanceAfter,
        string? reason,
        ReferenceType? referenceType,
        string? referenceId,
        Guid? lotId,
        string? idempotencyKey,
        Guid? reversalOfEntryId,
        Guid? createdBy,
        DateTimeOffset? occurredAtUtc = null)
    {
        Id = Guid.NewGuid();
        WalletId = walletId;
        Sequence = sequence;
        // Lưu giờ gốc của sự kiện theo chuẩn UTC nếu có
        CreatedAt = occurredAtUtc.HasValue && occurredAtUtc.Value != default
            ? occurredAtUtc.Value.ToUniversalTime()
            : DateTimeOffset.UtcNow;
        EntryType = entryType;
        Amount = amount;
        BalanceAfter = balanceAfter;
        Reason = reason;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        LotId = lotId;
        IdempotencyKey = idempotencyKey;
        ReversalOfEntryId = reversalOfEntryId;
        CreatedBy = createdBy;
    }
}
