namespace ParkingSystem.Contracts.Events.Payment;

// Thông báo cho bãi xe nâng thanh chắn cổng ra
public sealed record ParkingSessionPaidIntegrationEvent(
    Guid SessionId,                     // Mã phiên đỗ xe đã trả tiền
    Guid PaymentId,                     // Mã giao dịch thanh toán thành công
    long AmountPaid,                    // Số tiền thực tế trừ từ ví
    DateTimeOffset PaidAtUtc,           // Giờ thanh toán theo giờ
    DateTimeOffset ExitWindowExpiresUtc // Hạn chót xe phải ra khỏi bãi
);
