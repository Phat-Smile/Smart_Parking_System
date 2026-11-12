namespace ParkingSystem.Payment.Domain.Constants;

// Quản lý tập trung các hình thức thanh toán tiền đỗ xe
public static class PaymentMethod
{
    public const string Wallet = "Wallet"; // Thanh toán bằng ví cá nhân
    public const string VnPay = "VNPay";   // Thanh toán trực tiếp qua cổng VNPay
    public const string Cash = "Cash";     // Thanh toán tiền mặt tại cổng
}
