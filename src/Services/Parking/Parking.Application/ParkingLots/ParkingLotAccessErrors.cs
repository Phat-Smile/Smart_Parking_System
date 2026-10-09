using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.ParkingLots;

public static class ParkingLotAccessErrors
{
    // TECH-12: có role Staff/Owner nhưng không được gắn với bãi này.
    public static readonly Error AccessDenied =
        new("parking.lot.access_denied", "You are not assigned to this parking lot.");
}
