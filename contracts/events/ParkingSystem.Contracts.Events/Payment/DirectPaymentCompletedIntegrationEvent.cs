namespace ParkingSystem.Contracts.Events.Payment;

/// <summary>
/// Sự kiện thông báo khách hàng thanh toán phí đỗ xe trực tiếp qua VNPay thành công.
/// </summary>
public sealed record DirectPaymentCompletedIntegrationEvent(
    Guid TransactionId,
    string TxnRef,
    Guid? WalletId,
    decimal Amount,
    string VnpayTxnNo,
    string? BankCode,
    DateTimeOffset CompletedAtUtc);
