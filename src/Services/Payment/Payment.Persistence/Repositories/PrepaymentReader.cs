using Microsoft.EntityFrameworkCore;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Persistence.Repositories;

/// <summary>
/// Đọc số tiền cọc hợp lệ đã trừ từ ví cho lượt đặt chỗ (Booking)
/// theo đúng quy tắc quyết toán cấn trừ cọc US-PAY-001 / US-PAY-005.
/// </summary>
public sealed class PrepaymentReader(PaymentDbContext dbContext) : IPrepaymentReader
{
    public async Task<long> GetPrepaidAmountAsync(
        Guid accountId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty || bookingId == Guid.Empty)
        {
            return 0L;
        }

        var bookingIdStr = bookingId.ToString();
        var bookingIdGuidStr = bookingId.ToString("D");
        var bookingIdCompactStr = bookingId.ToString("N");

        // Tìm ví của tài khoản người dùng
        var walletId = await dbContext.Wallets
            .AsNoTracking()
            .Where(w => w.UserId == accountId)
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!walletId.HasValue)
        {
            return 0L;
        }

        // Lấy danh sách giao dịch sổ cái liên quan đến lượt đặt chỗ này
        var entries = await dbContext.WalletLedgerEntries
            .AsNoTracking()
            .Where(e => e.WalletId == walletId.Value
                     && e.ReferenceType == ReferenceType.Reservation
                     && (e.ReferenceId == bookingIdStr || e.ReferenceId == bookingIdGuidStr || e.ReferenceId == bookingIdCompactStr))
            .Select(e => new { e.EntryType, e.Amount })
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
        {
            return 0L;
        }

        return CalculateNetPrepaid(entries.Select(e => (e.EntryType, e.Amount)));
    }

    // Tính số tiền cọc còn lại chưa xử lý
    public static long CalculateNetPrepaid(IEnumerable<(LedgerEntryType EntryType, decimal Amount)> entries)
    {
        decimal netPrepaid = 0m;
        foreach (var entry in entries)
        {
            if (entry.EntryType == LedgerEntryType.Prepayment || (entry.Amount < 0 && entry.EntryType != LedgerEntryType.FeePayment))
            {
                netPrepaid += -entry.Amount;
            }
            else if (entry.EntryType is LedgerEntryType.Refund
                     or LedgerEntryType.PrepaymentForfeited
                     or LedgerEntryType.PrepaymentApplied)
            {
                netPrepaid -= entry.Amount;
            }
        }

        return netPrepaid > 0 ? (long)netPrepaid : 0L;
    }
}
