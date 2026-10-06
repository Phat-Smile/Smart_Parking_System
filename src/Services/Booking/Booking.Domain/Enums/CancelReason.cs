namespace ParkingSystem.Booking.Domain.Enums;

public enum CancelReason
{
    CustomerCancelled = 1,
    PaymentFailed = 2,
    NoShow = 3,
    NoSuitableSlot = 4
}
