namespace ParkingSystem.Parking.Domain.ParkingLayout;

// The status the system uses and shows (context 6.2), derived from the three slot layers.
public enum SlotEffectiveStatus
{
    Available = 1,
    Occupied = 2,
    Maintenance = 3,
    Unavailable = 4,
    Assigned = 5,
    NeedsVerification = 6
}
