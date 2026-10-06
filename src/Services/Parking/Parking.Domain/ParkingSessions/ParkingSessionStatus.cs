namespace ParkingSystem.Parking.Domain.ParkingSessions;

// BR-11.a: Active (in the lot, not fully paid), Paid (waiting to exit), Completed (left).
// Cancelled is a team decision on top of BR-11 (lead review, 2026-10-06): staff undo a session when the
// barrier was opened but the vehicle did not enter. The record is kept for the audit trail.
public enum ParkingSessionStatus
{
    Active = 1,
    Paid = 2,
    Completed = 3,
    Cancelled = 4
}
