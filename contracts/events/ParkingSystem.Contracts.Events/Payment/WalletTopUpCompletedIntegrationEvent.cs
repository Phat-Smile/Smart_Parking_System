namespace ParkingSystem.Contracts.Events.Payment;

// Sự kiện thông báo nạp ví thành công
public sealed record WalletTopUpCompletedIntegrationEvent(
    Guid WalletId,
    Guid UserId,
    decimal Amount,
    decimal BalanceAfter,
    string TxnRef,
    DateTimeOffset CompletedAtUtc);
