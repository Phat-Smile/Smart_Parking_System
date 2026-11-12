using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Domain.Entities;

public class VnpayTransaction
{
    public Guid Id { get; private set; }

    // Mã tham chiếu gửi sang VNPay
    // Cho phép trang return của VNPay lấy thẳng topupId từ URL mà không cần query thêm
    public string TxnRef { get; private set; } = null!;

    // Khóa ngoại trỏ về ví thụ hưởng
    public Guid WalletId { get; private set; }

    // Số tiền giao dịch (VND)
    public decimal Amount { get; private set; }

    // Mục đích giao dịch
    public VnpayTransactionPurpose Purpose { get; private set; }

    // Trạng thái hiện tại của giao dịch
    public VnpayTransactionStatus Status { get; private set; }

    // Khóa chống trùng lặp do client gửi qua header Idempotency-Key (8-64 ký tự)
    public string ClientRequestKey { get; private set; } = null!;

    // Thời điểm tạo yêu cầu nạp (UTC)
    public DateTimeOffset CreatedAt { get; private set; }

    // Thời điểm hết hạn thanh toán trên cổng VNPay (UTC)
    public DateTimeOffset ExpiresAt { get; private set; }

    // Thời điểm hoàn tất giao dịch khi IPN callback về (UTC)
    public DateTimeOffset? CompletedAt { get; private set; }

    // Thông tin đối soát do cổng VNPay trả về (phục vụ US-PAY-008)
    public string? VnpayTransactionNo { get; private set; }
    public string? VnpayBankCode { get; private set; }
    public string? VnpayResponseCode { get; private set; }

    private VnpayTransaction() { }

    /// <summary>
    /// Khởi tạo một giao dịch nạp ví mới ở trạng thái Pending.
    /// Toàn bộ bất biến (Invariants) của Domain được bảo vệ chặt chẽ tại đây.
    /// </summary>
    public static VnpayTransaction CreateTopup(
        Guid id,
        string txnRef,
        Guid walletId,
        decimal amount,
        string clientRequestKey,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(DomainErrorMessages.VnpayTransaction.EmptyTransactionId, nameof(id));
        }

        if (string.IsNullOrWhiteSpace(txnRef) || txnRef.Length != 32)
        {
            throw new ArgumentException(DomainErrorMessages.VnpayTransaction.InvalidTxnRef, nameof(txnRef));
        }

        if (walletId == Guid.Empty)
        {
            throw new ArgumentException(DomainErrorMessages.VnpayTransaction.InvalidWalletId, nameof(walletId));
        }

        if (amount <= 0m || amount % 1m != 0m)
        {
            throw new ArgumentException(DomainErrorMessages.VnpayTransaction.InvalidAmount, nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(clientRequestKey) || clientRequestKey.Length < 8 || clientRequestKey.Length > 64)
        {
            throw new ArgumentException(DomainErrorMessages.VnpayTransaction.InvalidClientRequestKey, nameof(clientRequestKey));
        }

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException(DomainErrorMessages.VnpayTransaction.InvalidExpiryTime, nameof(expiresAt));
        }

        return new VnpayTransaction
        {
            Id = id,
            TxnRef = txnRef,
            WalletId = walletId,
            Amount = amount,
            Purpose = VnpayTransactionPurpose.WalletTopUp,
            Status = VnpayTransactionStatus.Pending,
            ClientRequestKey = clientRequestKey,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt
        };
    }

    /// <summary>
    /// Khởi tạo một giao dịch thanh toán trực tiếp phí đỗ xe qua VNPay ở trạng thái Pending.
    /// </summary>
    public static VnpayTransaction CreateDirectPayment(
        Guid id,
        string txnRef,
        Guid sessionId,
        decimal amount,
        string clientRequestKey,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(DomainErrorMessages.VnpayTransaction.EmptyTransactionId, nameof(id));
        }

        if (string.IsNullOrWhiteSpace(txnRef) || txnRef.Length != 32)
        {
            throw new ArgumentException(DomainErrorMessages.VnpayTransaction.InvalidTxnRef, nameof(txnRef));
        }

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException(DomainErrorMessages.VnpayTransaction.InvalidWalletId, nameof(sessionId));
        }

        if (amount <= 0m || amount % 1m != 0m)
        {
            throw new ArgumentException(DomainErrorMessages.VnpayTransaction.InvalidAmount, nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(clientRequestKey) || clientRequestKey.Length < 8 || clientRequestKey.Length > 64)
        {
            throw new ArgumentException(DomainErrorMessages.VnpayTransaction.InvalidClientRequestKey, nameof(clientRequestKey));
        }

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException(DomainErrorMessages.VnpayTransaction.InvalidExpiryTime, nameof(expiresAt));
        }

        return new VnpayTransaction
        {
            Id = id,
            TxnRef = txnRef,
            WalletId = sessionId,
            Amount = amount,
            Purpose = VnpayTransactionPurpose.DirectPayment,
            Status = VnpayTransactionStatus.Pending,
            ClientRequestKey = clientRequestKey,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt
        };
    }

    /// <summary>
    /// Phương thức chuyển trạng thái thành công khi nhận IPN hợp lệ
    /// </summary>
    public void MarkAsSuccess(string vnpayTransactionNo, string? bankCode, string responseCode, DateTimeOffset completedAt)
    {
        if (Status != VnpayTransactionStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot mark transaction as success from status {Status}.");
        }

        Status = VnpayTransactionStatus.Success;
        VnpayTransactionNo = vnpayTransactionNo;
        VnpayBankCode = bankCode;
        VnpayResponseCode = responseCode;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Phương thức chuyển trạng thái thất bại khi VNPay báo lỗi hoặc hủy giao dịch
    /// </summary>
    public void MarkAsFailed(string? vnpayTransactionNo, string? bankCode, string responseCode, DateTimeOffset completedAt)
    {
        if (Status != VnpayTransactionStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot mark transaction as failed from status {Status}.");
        }

        Status = VnpayTransactionStatus.Failed;
        VnpayTransactionNo = vnpayTransactionNo;
        VnpayBankCode = bankCode;
        VnpayResponseCode = responseCode;
        CompletedAt = completedAt;
    }
}
