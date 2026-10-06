namespace ParkingSystem.Parking.Domain.ParkingSessions;

// BR-11.a: Active (in the lot, not fully paid), Paid (waiting to exit), Completed (left).
public enum ParkingSessionStatus
{
    Active = 1,
    Paid = 2,
    Completed = 3
}
