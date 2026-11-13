using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Common.Constants;

public static class ReservationDepositErrors
{
    public static readonly Error UserIdInvalid =
        new("payment.user_id_invalid", "User ID is invalid.");

    public static readonly Error ReservationIdInvalid =
        new("payment.reservation_id_invalid", "Reservation ID is invalid.");

    public static readonly Error AmountInvalid =
        new("payment.amount_invalid", "Deposit amount must be greater than zero and an integer.");

    public static readonly Error WalletNotFound =
        new("payment.wallet_not_found", "Wallet not found for the specified user.");

    public static readonly Error InsufficientBalance =
        new("payment.wallet_insufficient_balance", "Wallet balance is not enough to pay deposit.");

    public static readonly Error WalletBusy =
        new("payment.wallet_busy", "Wallet is currently busy. Please retry.");

    public static readonly Error IdempotencyKeyReused =
        new("payment.idempotency_key_reused", "Idempotency key was already used with a different request.");

    public static readonly Error RequestInProgress =
        new("payment.request_in_progress", "A request with this idempotency key is still being processed.");

    // Ví bị khóa hoặc chưa kích hoạt
    public static readonly Error WalletNotActive =
        new("payment.wallet_not_active", "Wallet is not active.");

    // Đặt chỗ này đã được hoàn tiền trước đó
    public static readonly Error AlreadyRefunded =
        new("payment.reservation_already_refunded", "Reservation was already refunded.");

    // Chưa từng có bút toán trừ cọc cho đặt chỗ này
    public static readonly Error ReservationNotDebited =
        new("payment.reservation_not_debited", "No deposit record found for this reservation.");

    // Số tiền yêu cầu hoàn vượt quá số tiền cọc đã trừ
    public static readonly Error RefundAmountExceedsDeposit =
        new("payment.refund_amount_exceeds_deposit", "Refund amount exceeds debited deposit amount.");
}
