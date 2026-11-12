namespace ParkingSystem.Payment.Application.Features.ParkingFeePayments.Queries.GetParkingFeePaymentStatus;

// Kết quả tra cứu trạng thái hóa đơn thanh toán
public sealed record GetParkingFeePaymentStatusResponse(
    Guid TransactionId,        // Mã hóa đơn
    Guid SessionId,            // Mã phiên đỗ xe
    string Method,             // Hình thức: VNPay, Wallet, Cash
    string Status,             // Trạng thái hiện tại
    long Amount,               // Số tiền đã trả
    string? PaymentUrl,        // Link QR chỉ khi đang chờ và chưa hết hạn
    DateTimeOffset? ExpiresAtUtc,         // Hạn quét mã QR
    DateTimeOffset? PaymentTimeUtc,       // Giờ chốt tính tiền (T)
    DateTimeOffset? ExitWindowExpiresUtc, // Hạn lái xe ra cổng
    DateTimeOffset? ConfirmedAtUtc,       // Giờ xác nhận thanh toán
    string? FailureCode                   // Mã lỗi khi thất bại
);
