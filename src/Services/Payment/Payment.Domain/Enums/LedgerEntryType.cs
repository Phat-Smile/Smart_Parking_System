namespace ParkingSystem.Payment.Domain.Enums;

public enum LedgerEntryType
{
    TopUp = 1,                  // Nạp tiền vào ví
    Prepayment = 2,             // Trừ tiền cọc đặt chỗ
    PrepaymentApplied = 3,      // Chuyển cọc thành phí đỗ xe
    PrepaymentForfeited = 4,    // Tịch thu cọc (khách không đến)
    FeePayment = 5,             // Trừ phí đỗ xe
    Refund = 6,                 // Hoàn tiền cho khách
    Adjustment = 7,             // Đền bù/cộng trừ lại tiền khi hệ thống tính sai
    BalanceCancellation = 8     // Hủy số dư (khi đóng ví)
}
