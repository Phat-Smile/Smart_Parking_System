namespace ParkingSystem.Booking.Application.Reservations;

public interface IReservationArrivalReminderTracker
{
    Task<bool> HasBeenDispatchedAsync(Guid reservationId, string reminderType, CancellationToken cancellationToken);
    Task MarkDispatchedAsync(Guid reservationId, string reminderType, CancellationToken cancellationToken);
}
