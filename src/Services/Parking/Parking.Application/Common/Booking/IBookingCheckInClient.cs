namespace ParkingSystem.Parking.Application.Common.Booking;

public interface IBookingCheckInClient
{
    Task<CheckedInReservation?> CheckInReservationAsync(
        Guid lotId,
        string normalizedPlate,
        DateTimeOffset entryTime,
        Guid sessionId,
        CancellationToken cancellationToken);
}

public sealed record CheckedInReservation(
    Guid ReservationId,
    Guid? UserId,
    Guid? VehicleId);
