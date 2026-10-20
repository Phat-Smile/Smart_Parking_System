namespace ParkingSystem.Parking.Domain.ParkingLayout;

// Slot layer 1 (BR-06.a): only slot sensor events change it. Null means no sensor event yet.
public enum SensorState
{
    Occupied = 1,
    Empty = 2
}
