namespace ParkingSystem.Parking.Domain.ParkingLayout;

// Slot layer 3 (BR-04, BR-06.a): created by the gate-entry flow and changed only by the slot transition table.
public enum DispatchState
{
    Assigned = 1,
    NeedsVerification = 2
}
