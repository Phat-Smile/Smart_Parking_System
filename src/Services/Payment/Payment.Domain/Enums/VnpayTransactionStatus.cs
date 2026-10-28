namespace ParkingSystem.Payment.Domain.Enums;

/// <summary>
/// Lưu các trạng thái Pending, Success, Failed, Expired
/// </summary>
public enum VnpayTransactionStatus
{
    Pending = 1,    // Đang chờ người dùng thanh toán qua cổng VNPay
    Success = 2,    // Đã thanh toán thành công (xác nhận qua IPN)
    Failed = 3,     // Giao dịch thất bại / lỗi thanh toán
    Expired = 4     // Hết thời hạn thanh toán (quá expiresAtUtc)
}
