namespace ParkingSystem.Parking.Domain.ParkingSessions;

/// <summary>
/// BR-11.c: a vehicle leaves only when the plate matches the session, the QR is the session's own QR and nothing is
/// left to pay. A plate match alone is never enough. One place for this rule, shared by the staff manual check
/// (US-STAFF-004) and the gate exit (US-PAY-006).
/// </summary>
public static class ExitEligibility
{
    // "Nothing left to pay" is Status == Paid and still inside the 15-minute exit window (BR-09.d).
    public static bool CanExit(ParkingSession session, bool plateMatches, bool qrMatches, DateTimeOffset arrivedAt) =>
        plateMatches
        && qrMatches
        && session.Status == ParkingSessionStatus.Paid
        && !session.IsExitWindowOver(arrivedAt);
}
