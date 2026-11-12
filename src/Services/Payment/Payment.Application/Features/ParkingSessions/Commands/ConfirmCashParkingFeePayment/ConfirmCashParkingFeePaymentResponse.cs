namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.ConfirmCashParkingFeePayment;

// Kết quả xác nhận thu tiền mặt thành công
public sealed record ConfirmCashParkingFeePaymentResponse(
    Guid TransactionId,
    Guid SessionId,
    long AmountPaid,
    string PaymentMethod,
    Guid ConfirmedByStaffId,
    Guid ParkingLotId,
    DateTimeOffset PaymentTimeUtc,
    DateTimeOffset ExitWindowExpiresUtc);
