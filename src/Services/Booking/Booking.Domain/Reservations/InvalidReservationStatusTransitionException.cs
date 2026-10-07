using ParkingSystem.Booking.Domain.Enums;

namespace ParkingSystem.Booking.Domain.Reservations;

public sealed class InvalidReservationStatusTransitionException : InvalidOperationException
{
    public ReservationStatus FromStatus { get; }
    public ReservationStatus ToStatus { get; }

    public InvalidReservationStatusTransitionException(ReservationStatus fromStatus, ReservationStatus toStatus)
        : base($"Cannot transition reservation status from '{fromStatus}' to '{toStatus}'. Transition is invalid or state is terminal.")
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }
}
