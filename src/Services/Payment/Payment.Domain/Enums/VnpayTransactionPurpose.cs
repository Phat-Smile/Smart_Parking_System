namespace ParkingSystem.Payment.Domain.Enums;

/// <summary>
/// Lưu mục đích giao dịch WalletTopUp, DirectPayment
/// </summary>
public enum VnpayTransactionPurpose
{
    WalletTopUp = 1,    // Nạp tiền vào ví cá nhân
    DirectPayment = 2   // Thanh toán phí đỗ xe trực tiếp tại cổng ra
}
