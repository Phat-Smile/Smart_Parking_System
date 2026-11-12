using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Common.Constants;

public static class ReservationDepositErrors
{
    public static readonly Error UserIdInvalid =
        new("payment.user_id_invalid", "User ID is invalid.");

    public static readonly Error ReservationIdInvalid =
        new("payment.reservation_id_invalid", "Reservation ID is invalid.");

    public static readonly Error AmountInvalid =
        new("payment.amount_invalid", "Deposit amount must be greater than zero.");

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
}
