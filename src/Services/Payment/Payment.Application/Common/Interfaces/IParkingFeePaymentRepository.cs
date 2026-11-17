using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Application.Common.Interfaces;

// Cổng lưu trữ hóa đơn thanh toán tiền gửi xe
public interface IParkingFeePaymentRepository
{
    // Kiểm tra xe đã trả tiền thành công trước đó chưa
    Task<bool> HasSucceededPaymentAsync(Guid sessionId, CancellationToken ct);

    // Kiểm tra thanh toán cũ để tương thích ngược
    Task<bool> ExistsForSessionAsync(Guid sessionId, CancellationToken ct);

    // Lấy giao dịch đang chờ thanh toán của phiên xe
    Task<ParkingFeePayment?> GetPendingBySessionIdAsync(Guid sessionId, CancellationToken ct);

    // Thay thế mã chờ thanh toán cũ bằng mã mới
    Task CreateOrReplacePendingAsync(ParkingFeePayment newPayment, CancellationToken ct);

    // Tìm hóa đơn qua mã gửi sang VNPay
    Task<ParkingFeePayment?> GetByTxnRefAsync(string txnRef, CancellationToken ct);

    // Tải lại dữ liệu mới nhất từ database
    Task ReloadAsync(ParkingFeePayment payment, CancellationToken ct);

    // Lưu các thay đổi vào cơ sở dữ liệu
    Task SaveChangesAsync(CancellationToken ct);

    // Lưu hóa đơn thanh toán mới vào cơ sở dữ liệu
    Task AddAsync(ParkingFeePayment payment, CancellationToken ct);

    // Tìm hóa đơn theo mã thanh toán
    Task<ParkingFeePayment?> GetByIdAsync(Guid id, CancellationToken ct);

    // Tìm hóa đơn thành công theo mã phiên xe hoặc mã giao dịch để xuất hóa đơn điện tử (US-PAY-003)
    Task<ParkingFeePayment?> GetReceiptPaymentAsync(Guid sessionOrPaymentId, CancellationToken ct);

    // Lấy hóa đơn thành công của phiên xe để cập nhật phụ phí quá giờ (BR-09.d)
    Task<ParkingFeePayment?> GetSucceededBySessionIdAsync(Guid sessionId, CancellationToken ct) =>
        GetReceiptPaymentAsync(sessionId, ct);

    // Tính toán tổng hợp doanh thu theo bãi và mốc thời gian (US-OWNER-010)
    Task<OwnerRevenueMetricsResult> GetRevenueMetricsAsync(
        IReadOnlyList<Guid> lotIds,
        DateTimeOffset todayStartUtc,
        DateTimeOffset weekStartUtc,
        DateTimeOffset monthStartUtc,
        CancellationToken ct) =>
        Task.FromResult(new OwnerRevenueMetricsResult(
            new Features.OwnerRevenue.Queries.GetOwnerRevenueSummary.RevenuePeriodMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0),
            new Features.OwnerRevenue.Queries.GetOwnerRevenueSummary.RevenuePeriodMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0),
            new Features.OwnerRevenue.Queries.GetOwnerRevenueSummary.RevenuePeriodMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0),
            new Features.OwnerRevenue.Queries.GetOwnerRevenueSummary.RevenuePeriodMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0)));
}
