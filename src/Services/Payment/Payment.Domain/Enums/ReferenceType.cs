namespace ParkingSystem.Payment.Domain.Enums;

public enum ReferenceType
{
    Reservation = 1,        // Nguyên nhân do: Mã Đặt chỗ trước (Booking)
    ParkingSession = 2,     // Nguyên nhân do: Lượt đỗ xe của khách (Session)
    VnpayTransaction = 3    // Nguyên nhân do: Giao dịch nạp tiền qua VNPay
}
