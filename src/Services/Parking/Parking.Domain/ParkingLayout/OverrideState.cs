namespace ParkingSystem.Parking.Domain.ParkingLayout;

// Slot layer 2 (BR-06.a, BR-06.b, BR-06.c): set by staff, or declared Unavailable in the layout.
public enum OverrideState
{
    Maintenance = 1,
    Unavailable = 2
}
