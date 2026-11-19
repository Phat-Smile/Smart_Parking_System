namespace ParkingSystem.Parking.Application.Common.Booking;

public interface IBookingCheckInClient
{
    // entryTime is the Booking contract name for the time used to match a reservation (trusted gate arrival when available).
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

/// <summary>
/// BR-02.f, context point 35 (option a, Thịnh 2026-11-17): a car arrived and the lot had no Available car slot, so Parking
/// did not check the reservation in. Booking cancels a Confirmed reservation of this plate at this lot with reason
/// NoSuitableSlot and refunds 100%; it stays Confirmed until then, so it is never stuck in Used without a slot.
/// </summary>
public interface IBookingNoSlotReporter
{
    /// <returns>The cancelled reservation, or null when the plate had none or Booking could not be reached.</returns>
    Task<Guid?> ReportNoSuitableSlotAsync(
        Guid lotId,
        string normalizedPlate,
        DateTimeOffset arrivedAt,
        CancellationToken cancellationToken);
}
