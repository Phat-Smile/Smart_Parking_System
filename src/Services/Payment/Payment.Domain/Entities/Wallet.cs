using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Domain.Entities;

public class Wallet
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public decimal Balance { get; private set; }
    public WalletStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    // Số thứ tự bút toán lớn nhất của ví này (tăng tuần tự 1, 2, 3...)
    public long LastSequence { get; private set; }

    // Không cho phép 2 giao dịch cùng sửa số dư trên cùng một phiên bản ví
    public long Version { get; private set; }

    // Danh sách gốc các dòng sổ cái
    private readonly List<WalletLedgerEntry> _ledgerEntries = new();

    // Bản copy chỉ đọc cho bên ngoài truy vấn danh sách giao dịch
    public IReadOnlyCollection<WalletLedgerEntry> LedgerEntries => _ledgerEntries.AsReadOnly();

    private Wallet() { }

    public static Wallet Create(Guid userId)
    {
        return new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Balance = 0m,
            Status = WalletStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            LastSequence = 0L,
            Version = 1 // Khởi tạo Version ban đầu bằng 1
        };
    }

    public void Close()
    {
        if (Status == WalletStatus.Closed)
        {
            return;
        }

        if (Balance != 0m)
        {
            throw new InvalidOperationException(DomainErrorMessages.Wallet.MustBeZeroToClose);
        }

        Status = WalletStatus.Closed;
        ClosedAt = DateTimeOffset.UtcNow;
        Version++; // Tăng Version khi đóng ví
    }

    // Biến động tiền hoặc ghi nhận sự kiện tài chính vào sổ cái
    public void AddEntry(
        LedgerEntryType entryType,              // Loại giao dịch
        decimal amount,                         // Số tiền
        string? reason = null,                  // Ghi chú lý do
        ReferenceType? referenceType = null,    // Loại chứng từ tham chiếu
        string? referenceId = null,             // ID chứng từ
        Guid? lotId = null,                     // ID bãi đỗ xe
        string? idempotencyKey = null,          // Mã chống trùng lặp (ví dụ mã giao dịch VNPay)
        Guid? reversalOfEntryId = null,         // Trỏ về dòng giao dịch bị sai (bút toán đảo)
        Guid? createdBy = null)                 // Người thực hiện giao dịch (phục vụ audit log)
    {
        if (Status != WalletStatus.Active)
        {
            throw new InvalidOperationException(DomainErrorMessages.Wallet.AlreadyClosed);
        }

        // Chặn giao dịch 0 đồng
        if (amount == 0m)
        {
            throw new ArgumentException(DomainErrorMessages.Wallet.AmountCannotBeZero, nameof(amount));
        }

        // Tiền VND không có số thập phân lẻ
        if (amount % 1m != 0m)
        {
            throw new ArgumentException(DomainErrorMessages.Wallet.AmountMustBeInteger, nameof(amount));
        }

        decimal newBalance = Balance;

        // Xử lý biến động số dư theo đúng quy tắc nghiệp vụ BR-12
        switch (entryType)
        {
            case LedgerEntryType.TopUp:     // Nạp tiền
            case LedgerEntryType.Refund:    // Hoàn tiền
                if (amount <= 0m)
                {
                    throw new ArgumentException(DomainErrorMessages.Wallet.AmountMustBePositive, nameof(amount));
                }
                newBalance = Balance + amount;
                break;

            case LedgerEntryType.FeePayment:            // Trừ phí đỗ xe
            case LedgerEntryType.Prepayment:            // Trừ tiền cọc đặt chỗ
            case LedgerEntryType.BalanceCancellation:   // Hủy số dư khi đóng ví
                if (amount >= 0m)
                {
                    throw new ArgumentException(DomainErrorMessages.Wallet.AmountMustBeNegative, nameof(amount));
                }
                newBalance = Balance + amount;
                break;

            case LedgerEntryType.PrepaymentApplied:     // Áp cọc thành phí đỗ xe (BR-12.c)
            case LedgerEntryType.PrepaymentForfeited:   // Tịch thu cọc khi không đến (BR-12.c)
                // Tiền cọc đã trừ từ trước, ở đây chỉ ghi nhận xử lý cọc nên không đổi số dư ví
                if (amount <= 0m)
                {
                    throw new ArgumentException(DomainErrorMessages.Wallet.AmountMustBePositive, nameof(amount));
                }
                newBalance = Balance; // Giữ nguyên số dư
                break;

            case LedgerEntryType.Adjustment: // Kế toán điều chỉnh (có thể cộng hoặc trừ)
                newBalance = Balance + amount;
                break;

            default:
                throw new InvalidOperationException($"Ledger entry type {entryType} does not have balance rules configured.");
        }

        // Kiểm tra số dư ví không được âm
        if (newBalance < 0m)
        {
            throw new InvalidOperationException(DomainErrorMessages.Wallet.NotEnoughMoney);
        }

        // Tăng số thứ tự bút toán cho riêng ví này
        var sequence = ++LastSequence;

        // Tạo dòng sổ cái ghi nhận giao dịch
        var entry = new WalletLedgerEntry(
            walletId: Id,
            sequence: sequence,
            entryType: entryType,
            amount: amount,
            balanceAfter: newBalance,
            reason: reason,
            referenceType: referenceType,
            referenceId: referenceId,
            lotId: lotId,
            idempotencyKey: idempotencyKey,
            reversalOfEntryId: reversalOfEntryId,
            createdBy: createdBy
        );

        _ledgerEntries.Add(entry);
        Balance = newBalance;
        Version++; // Tăng Version sau mỗi lần biến động ví
    }
}
