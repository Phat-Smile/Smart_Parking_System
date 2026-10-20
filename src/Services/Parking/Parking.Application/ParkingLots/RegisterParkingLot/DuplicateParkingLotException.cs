namespace ParkingSystem.Parking.Application.ParkingLots.RegisterParkingLot;

/// <summary>A parking lot with this name has already been registered by the same owner.</summary>
public sealed class DuplicateParkingLotException()
    : Exception("This owner already has a registered parking lot with this name.");
