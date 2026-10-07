namespace ParkingSystem.Parking.Domain.ParkingSessions;

// Derived from the session, never sent by the caller (MAIN FLOW MF-08, MF-09A, MF-09B).
// Having an account is independent of being a walk-in; a booking always belongs to an account.
public enum CustomerType
{
    Booking = 1,
    RegisteredWalkIn = 2,
    UnregisteredWalkIn = 3
}
