namespace ParkingSystem.Booking.Application.Reservations.Commands.CancelForNoSuitableSlot;

public sealed record CancelForNoSuitableSlotResult(
    bool Cancelled,
    Guid? ReservationId);
