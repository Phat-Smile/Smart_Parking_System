namespace ParkingSystem.Parking.Domain.ParkingSessions;

/// <summary>
/// BR-11.c: a vehicle leaves only when the plate matches the session, the QR is the session's own QR and nothing is
/// left to pay. A plate match alone is never enough. One place for this rule, shared by the staff manual check
/// (US-STAFF-004) and the gate exit (US-PAY-006).
/// </summary>
public static class ExitEligibility
{
    // "Nothing left to pay" is read as Status == Paid for now. The 15-minute rule after payment (BR-09.d) that sends a
    // Paid session back to Active belongs to US-PAY-006 and will be added here.
    public static bool CanExit(ParkingSession session, bool plateMatches, bool qrMatches) =>
        plateMatches && qrMatches && session.Status == ParkingSessionStatus.Paid;
}
