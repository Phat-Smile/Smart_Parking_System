namespace ParkingSystem.Payment.Application.Common.Interfaces;

// Cổng tạo đường dẫn thanh toán qua VNPay
public interface IVnpayPaymentGateway
{
    // Tạo liên kết thanh toán VNPay có chữ ký số
    string BuildPaymentUrl(
        string txnRef, // Mã giao dịch gửi sang VNPay
        long amountVnd, // Số tiền cần thanh toán
        string orderInfo, // Nội dung hiển thị khi thanh toán
        DateTimeOffset createdAtUtc, // Thời gian tạo giao dịch
        DateTimeOffset expiresAtUtc, // Thời gian hết hạn quét mã
        string clientIp); // Địa chỉ IP người dùng
}
