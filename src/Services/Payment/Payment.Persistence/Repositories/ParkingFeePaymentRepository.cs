using Microsoft.EntityFrameworkCore;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Constants;
using ParkingSystem.Payment.Domain.Entities;

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

    // Tìm hóa đơn thành công theo mã phiên xe hoặc mã giao dịch để xuất hóa đơn điện tử (US-PAY-003)
    public async Task<ParkingFeePayment?> GetReceiptPaymentAsync(Guid sessionOrPaymentId, CancellationToken ct)
    {
        return await dbContext.Set<ParkingFeePayment>()
            .AsNoTracking()
            .Where(p => (p.SessionId == sessionOrPaymentId || p.Id == sessionOrPaymentId) && p.Status == PaymentStatus.Succeeded)
            .OrderByDescending(p => p.PaymentTimeUtc)
            .FirstOrDefaultAsync(ct);
    }

    // Lấy hóa đơn thành công của phiên xe để cập nhật phụ phí quá giờ (BR-09.d)
    public async Task<ParkingFeePayment?> GetSucceededBySessionIdAsync(Guid sessionId, CancellationToken ct)
    {
        return await dbContext.Set<ParkingFeePayment>()
            .FirstOrDefaultAsync(p => p.SessionId == sessionId && p.Status == PaymentStatus.Succeeded, ct);
    }
}
