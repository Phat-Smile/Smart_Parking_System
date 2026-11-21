using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Domain.Entities;

public class RefundRequest
{
    public Guid Id { get; private set; }
    public Guid WalletId { get; private set; }
    public Guid LotId { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = null!;
    public ReferenceType? ReferenceType { get; private set; }
    public string? ReferenceId { get; private set; }
    public RefundStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? OwnerConfirmedBy { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public Guid? LedgerEntryId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private RefundRequest() { }

    public static RefundRequest Create(
        Guid walletId,
        Guid lotId,
        decimal amount,
        string reason,
        Guid createdBy,
        ReferenceType? referenceType = null,
        string? referenceId = null,
        DateTimeOffset? now = null)
    {
        if (walletId == Guid.Empty)
            throw new ArgumentException("WalletId must not be empty.", nameof(walletId));
        if (lotId == Guid.Empty)
            throw new ArgumentException("LotId must not be empty.", nameof(lotId));
        if (createdBy == Guid.Empty)
            throw new ArgumentException("CreatedBy must not be empty.", nameof(createdBy));
        if (amount <= 0 || amount != Math.Truncate(amount))
            throw new ArgumentException("Amount must be a positive whole number in VND.", nameof(amount));
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new ArgumentException("Reason is required and must not exceed 500 characters.", nameof(reason));
        if (referenceId != null && referenceId.Length > 100)
            throw new ArgumentException("ReferenceId must not exceed 100 characters.", nameof(referenceId));

        return new RefundRequest
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            LotId = lotId,
            Amount = amount,
            Reason = reason.Trim(),
            ReferenceType = referenceType,
            ReferenceId = referenceId?.Trim(),
            Status = RefundStatus.Requested,
            CreatedBy = createdBy,
            OwnerConfirmedBy = null,
            ReviewedBy = null,
            ReviewedAt = null,
            LedgerEntryId = null,
            CreatedAt = now ?? DateTimeOffset.UtcNow
        };
    }

    public void ConfirmByOwner(Guid ownerId, DateTimeOffset now)
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException("OwnerId must not be empty.", nameof(ownerId));
        if (Status != RefundStatus.Requested)
            throw new InvalidOperationException("Only refund requests in Requested status can be confirmed by an owner.");
        if (OwnerConfirmedBy != null)
            throw new InvalidOperationException("Refund request has already been confirmed by an owner.");

        OwnerConfirmedBy = ownerId;
    }

    public void Reject(Guid adminId, string reason, DateTimeOffset now)
    {
        if (adminId == Guid.Empty)
            throw new ArgumentException("AdminId must not be empty.", nameof(adminId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required when rejecting a refund request.", nameof(reason));
        if (Status != RefundStatus.Requested)
            throw new InvalidOperationException("Only refund requests in Requested status can be rejected.");
        if (OwnerConfirmedBy == null)
            throw new InvalidOperationException("Owner has not confirmed this refund request.");
        if (adminId == CreatedBy)
            throw new UnauthorizedAccessException("Creator of the refund request cannot be the reviewer.");

        Status = RefundStatus.Rejected;
        ReviewedBy = adminId;
        ReviewedAt = now;
    }
}
