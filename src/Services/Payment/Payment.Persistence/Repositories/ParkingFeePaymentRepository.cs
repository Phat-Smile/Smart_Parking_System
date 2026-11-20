using Microsoft.EntityFrameworkCore;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Features.OwnerRevenue.Queries.GetOwnerRevenueSummary;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Persistence.Repositories;

// Kho lưu trữ hóa đơn thanh toán tiền xe
public sealed class ParkingFeePaymentRepository(PaymentDbContext dbContext) : IParkingFeePaymentRepository
{
    // Kiểm tra phiên xe đã trả tiền thành công chưa
    public async Task<bool> HasSucceededPaymentAsync(Guid sessionId, CancellationToken ct)
    {
        return await dbContext.Set<ParkingFeePayment>()
            .AsNoTracking()
            .AnyAsync(p => p.SessionId == sessionId && p.Status == PaymentStatus.Succeeded, ct);
    }

    // Kiểm tra tương thích ngược với các lệnh cũ
    public async Task<bool> ExistsForSessionAsync(Guid sessionId, CancellationToken ct)
    {
        return await HasSucceededPaymentAsync(sessionId, ct);
    }

    // Lấy giao dịch chờ thanh toán của phiên xe
    public async Task<ParkingFeePayment?> GetPendingBySessionIdAsync(Guid sessionId, CancellationToken ct)
    {
        return await dbContext.Set<ParkingFeePayment>()
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.Status == PaymentStatus.Pending, ct);
    }

    // Hủy mã cũ và thêm mã chờ thanh toán mới
    public async Task CreateOrReplacePendingAsync(ParkingFeePayment newPayment, CancellationToken ct)
    {
        // Hủy các mã chờ cũ của cùng phiên xe
        await dbContext.Set<ParkingFeePayment>()
            .Where(p => p.SessionId == newPayment.SessionId && p.Status == PaymentStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Superseded), ct);

        try
        {
            await dbContext.Set<ParkingFeePayment>().AddAsync(newPayment, ct);
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Bỏ theo dõi bản ghi lỗi để bảo vệ phiên làm việc
            dbContext.Entry(newPayment).State = EntityState.Detached;
            throw;
        }
    }

    // Thêm hóa đơn thanh toán mới vào bộ theo dõi
    public async Task AddAsync(ParkingFeePayment payment, CancellationToken ct)
    {
        await dbContext.Set<ParkingFeePayment>().AddAsync(payment, ct);
    }

    // Tìm hóa đơn qua mã gửi sang VNPay
    public async Task<ParkingFeePayment?> GetByTxnRefAsync(string txnRef, CancellationToken ct)
    {
        return await dbContext.Set<ParkingFeePayment>()
            .FirstOrDefaultAsync(p => p.TxnRef == txnRef, ct);
    }

    // Tải lại dữ liệu mới nhất từ database
    public async Task ReloadAsync(ParkingFeePayment payment, CancellationToken ct)
    {
        await dbContext.Entry(payment).ReloadAsync(ct);
    }

    // Lưu các thay đổi vào cơ sở dữ liệu
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await dbContext.SaveChangesAsync(ct);
    }

    // Tìm hóa đơn theo mã thanh toán
    public async Task<ParkingFeePayment?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await dbContext.Set<ParkingFeePayment>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    // Tìm hóa đơn thành công mới nhất để xuất biên lai (US-PAY-003)
    public async Task<ParkingFeePayment?> GetReceiptPaymentAsync(Guid sessionOrPaymentId, CancellationToken ct)
    {
        return await dbContext.Set<ParkingFeePayment>()
            .AsNoTracking()
            .Where(p => (p.SessionId == sessionOrPaymentId || p.Id == sessionOrPaymentId) && p.Status == PaymentStatus.Succeeded)
            .OrderByDescending(p => p.PaymentSequence)
            .ThenByDescending(p => p.PaymentTimeUtc)
            .FirstOrDefaultAsync(ct);
    }

    // Lấy hóa đơn thành công mới nhất của phiên (BR-09.d)
    public async Task<ParkingFeePayment?> GetSucceededBySessionIdAsync(Guid sessionId, CancellationToken ct)
    {
        return await dbContext.Set<ParkingFeePayment>()
            .AsNoTracking()
            .Where(p => p.SessionId == sessionId && p.Status == PaymentStatus.Succeeded)
            .OrderByDescending(p => p.PaymentSequence)
            .ThenByDescending(p => p.PaymentTimeUtc)
            .FirstOrDefaultAsync(ct);
    }

    // Xóa dữ liệu tạm chưa lưu trước khi xử lý xung đột
    public void ClearChangeTracker() => dbContext.ChangeTracker.Clear();

    // Tính toán tổng hợp doanh thu 1 round-trip theo từng mốc thời gian
    public async Task<OwnerRevenueMetricsResult> GetRevenueMetricsAsync(
        IReadOnlyList<Guid> lotIds,
        DateTimeOffset todayStartUtc,
        DateTimeOffset weekStartUtc,
        DateTimeOffset monthStartUtc,
        CancellationToken ct)
    {
        var query = dbContext.Set<ParkingFeePayment>()
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Succeeded);

        if (lotIds.Count == 1)
        {
            var singleLot = lotIds[0];
            query = query.Where(p => p.ParkingLotId == singleLot);
        }
        else if (lotIds.Count > 1)
        {
            query = query.Where(p => lotIds.Contains(p.ParkingLotId));
        }

        var grouped = await query
            .GroupBy(p => p.PaymentMethod)
            .Select(g => new
            {
                Method = g.Key,
                TodayAmount = g.Where(x => x.PaymentTimeUtc >= todayStartUtc).Sum(x => (long?)x.AmountPaid) ?? 0L,
                TodayCount = g.Count(x => x.PaymentTimeUtc >= todayStartUtc),
                WeekAmount = g.Where(x => x.PaymentTimeUtc >= weekStartUtc).Sum(x => (long?)x.AmountPaid) ?? 0L,
                WeekCount = g.Count(x => x.PaymentTimeUtc >= weekStartUtc),
                MonthAmount = g.Where(x => x.PaymentTimeUtc >= monthStartUtc).Sum(x => (long?)x.AmountPaid) ?? 0L,
                MonthCount = g.Count(x => x.PaymentTimeUtc >= monthStartUtc),
                AllTimeAmount = g.Sum(x => (long?)x.AmountPaid) ?? 0L,
                AllTimeCount = g.Count()
            })
            .ToListAsync(ct);

        // Cộng dồn tiền cọc bị mất từ sổ cái theo bãi
        var forfeitQuery = dbContext.WalletLedgerEntries
            .AsNoTracking()
            .Where(e => e.EntryType == LedgerEntryType.PrepaymentForfeited);

        if (lotIds.Count == 1)
        {
            var singleLot = lotIds[0];
            forfeitQuery = forfeitQuery.Where(e => e.LotId == singleLot);
        }
        else if (lotIds.Count > 1)
        {
            forfeitQuery = forfeitQuery.Where(e => e.LotId.HasValue && lotIds.Contains(e.LotId.Value));
        }

        var forfeited = await forfeitQuery
            .GroupBy(e => e.EntryType)
            .Select(g => new
            {
                TodayAmount = g.Where(x => x.CreatedAt >= todayStartUtc).Sum(x => (decimal?)x.Amount) ?? 0m,
                WeekAmount = g.Where(x => x.CreatedAt >= weekStartUtc).Sum(x => (decimal?)x.Amount) ?? 0m,
                MonthAmount = g.Where(x => x.CreatedAt >= monthStartUtc).Sum(x => (decimal?)x.Amount) ?? 0m,
                AllTimeAmount = g.Sum(x => (decimal?)x.Amount) ?? 0m
            })
            .FirstOrDefaultAsync(ct);

        long GetAmount(Func<dynamic, long> selector, string method) =>
            grouped.Where(x => string.Equals(x.Method, method, StringComparison.OrdinalIgnoreCase))
                   .Select(selector)
                   .FirstOrDefault();

        int GetCount(Func<dynamic, int> selector, string method) =>
            grouped.Where(x => string.Equals(x.Method, method, StringComparison.OrdinalIgnoreCase))
                   .Select(selector)
                   .FirstOrDefault();

        RevenuePeriodMetrics BuildMetrics(
            Func<dynamic, long> amountSelector,
            Func<dynamic, int> countSelector,
            long forfeitedAmt)
        {
            var cashAmt = GetAmount(amountSelector, PaymentMethod.Cash);
            var vnpayAmt = GetAmount(amountSelector, PaymentMethod.VnPay);
            var walletAmt = GetAmount(amountSelector, PaymentMethod.Wallet);
            var onlineAmt = vnpayAmt + walletAmt;
            var totalAmt = cashAmt + onlineAmt + forfeitedAmt;

            var cashCnt = GetCount(countSelector, PaymentMethod.Cash);
            var vnpayCnt = GetCount(countSelector, PaymentMethod.VnPay);
            var walletCnt = GetCount(countSelector, PaymentMethod.Wallet);
            var onlineCnt = vnpayCnt + walletCnt;
            var totalCnt = cashCnt + onlineCnt;

            return new RevenuePeriodMetrics(
                TotalRevenue: totalAmt,
                CashRevenue: cashAmt,
                OnlineRevenue: onlineAmt,
                VnpayRevenue: vnpayAmt,
                WalletRevenue: walletAmt,
                ForfeitedDepositRevenue: forfeitedAmt,
                TransactionCount: totalCnt,
                CashTransactionCount: cashCnt,
                OnlineTransactionCount: onlineCnt);
        }

        return new OwnerRevenueMetricsResult(
            Today: BuildMetrics(x => (long)x.TodayAmount, x => (int)x.TodayCount, (long)(forfeited?.TodayAmount ?? 0m)),
            ThisWeek: BuildMetrics(x => (long)x.WeekAmount, x => (int)x.WeekCount, (long)(forfeited?.WeekAmount ?? 0m)),
            ThisMonth: BuildMetrics(x => (long)x.MonthAmount, x => (int)x.MonthCount, (long)(forfeited?.MonthAmount ?? 0m)),
            AllTime: BuildMetrics(x => (long)x.AllTimeAmount, x => (int)x.AllTimeCount, (long)(forfeited?.AllTimeAmount ?? 0m)));
    }
}
