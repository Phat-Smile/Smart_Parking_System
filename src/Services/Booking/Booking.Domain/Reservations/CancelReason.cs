namespace ParkingSystem.Booking.Domain.Reservations;

public enum CancelReason
{
    CustomerCancelled,
    PaymentFailed,
    NoShow,
    NoSuitableSlot
}
