namespace ParkingSystem.Payment.Domain.Enums;

// Mục đích trừ cọc hoặc thanh toán trước cho đặt chỗ
public enum ReservationPrepaymentPurpose
{
    // Cọc giữ chỗ vãng lai
    Deposit = 1,

    // Trả trước đặt chỗ theo lịch
    Scheduled = 2
}
