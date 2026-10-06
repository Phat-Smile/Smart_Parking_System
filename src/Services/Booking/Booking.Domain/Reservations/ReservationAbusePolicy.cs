namespace ParkingSystem.Booking.Domain.Reservations;

/// <summary>
/// Domain policy for US-RES-010 — Reservation Abuse Limits (BR-01.f).
/// </summary>
public static class ReservationAbusePolicy
{
    public const int DefaultMaxActiveReservationsPerAccount = 3;

    /// <summary>
    /// Validates whether the account has reached the maximum permitted active reservations.
    /// Returns true if valid (under the limit), false if limit reached/exceeded.
    /// </summary>
    public static bool CanAccountCreateReservation(
        int currentActiveReservationCount,
        int maxActiveReservations = DefaultMaxActiveReservationsPerAccount)
    {
        return currentActiveReservationCount < maxActiveReservations;
    }

    /// <summary>
    /// Validates whether the vehicle plate has any active reservation overlapping the requested time window.
    /// Returns true if no active reservation overlaps (allowed), false if overlap found (rejected).
    /// </summary>
    public static bool CanPlateReserveTimeWindow(
        IEnumerable<Reservation> activeReservationsForPlate,
        ReservationTimeWindow requestedWindow,
        DateTimeOffset utcNow)
    {
        foreach (var reservation in activeReservationsForPlate)
        {
            if (!reservation.IsActive(utcNow))
                continue;

            var existingWindow = reservation.GetReservationWindow();
            if (existingWindow.Overlaps(requestedWindow))
            {
                return false;
            }
        }

        return true;
    }
}
