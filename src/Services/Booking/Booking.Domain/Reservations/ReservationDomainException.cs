namespace ParkingSystem.Booking.Domain.Reservations;

public sealed class ReservationDomainException : Exception
{
    public ReservationDomainException(string message) : base(message)
    {
    }

    public ReservationDomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
