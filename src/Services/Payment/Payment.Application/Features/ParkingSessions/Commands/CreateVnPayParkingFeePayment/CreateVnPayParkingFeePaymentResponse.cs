namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.CreateVnPayParkingFeePayment;

// Kết quả trả về sau khi tạo link thanh toán
public sealed record CreateVnPayParkingFeePaymentResponse(
    Guid TransactionId, // Mã hóa đơn trong hệ thống
    string PaymentUrl, // Đường dẫn sang trang VNPay
    long Amount, // Số tiền thực tế phải trả
    DateTimeOffset ExpiresAtUtc, // Thời gian hết hạn quét mã
    string Status, // Trạng thái giao dịch
    bool IsReused = false, // Tái sử dụng lại mã cũ còn hạn
    bool IsReplayed = false // Trả lại kết quả cũ do trùng khóa
);
