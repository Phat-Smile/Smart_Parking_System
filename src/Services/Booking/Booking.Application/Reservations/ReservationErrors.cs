using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Reservations;

public static class ReservationErrors
{
    public static readonly Error NotFound =
        new("booking.reservation.not_found", "The reservation was not found.");

    public static readonly Error AccessDenied =
        new("booking.reservation.access_denied", "You do not have permission to access this reservation.");

    public static readonly Error InvalidStatus =
        new("booking.reservation.invalid_status", "The reservation is not in a valid state for this action.");

    public static readonly Error HoldExpired =
        new("booking.reservation.hold_expired", "The temporary hold has expired.");

    public static readonly Error AlreadyConfirmed =
        new("booking.reservation.already_confirmed", "The reservation is already confirmed.");

    public static readonly Error CannotCancel =
        new("booking.reservation.cannot_cancel", "The reservation cannot be cancelled in its current state.");

    public static readonly Error PaymentNotAvailable =
        new("booking.reservation.payment_not_available", "Chưa thể xác nhận đặt chỗ vì chức năng thanh toán tiền cọc chưa sẵn sàng. Bạn chưa bị tính phí và giữ chỗ tạm sẽ tự hết hạn.");

    public static readonly Error InsufficientWalletBalance =
        new("booking.reservation.insufficient_wallet_balance", "Wallet balance is insufficient to pay the reservation deposit.");

    public static readonly Error PaymentFailed =
        new("booking.reservation.payment_failed", "Payment for the reservation deposit failed.");

    public static readonly Error PaymentUnconfirmed =
        new("booking.reservation.payment_unconfirmed", "Payment status for the reservation deposit is unconfirmed. Please retry or wait for reconciliation.");
}
