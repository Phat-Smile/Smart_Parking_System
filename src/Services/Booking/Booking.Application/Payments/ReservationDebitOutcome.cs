namespace ParkingSystem.Booking.Application.Payments;

public enum ReservationDebitOutcome
{
    Unspecified = 0,
    Succeeded = 1,
    InsufficientBalance = 2,
    WalletNotActive = 3,
    AmountMismatch = 4
}

