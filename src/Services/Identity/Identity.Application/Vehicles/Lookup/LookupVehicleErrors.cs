using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Identity.Application.Vehicles.Lookup;

public static class LookupVehicleErrors
{
    public static readonly Error EmptyPlate =
        new("identity.plate_empty", "license_plate must not be empty or whitespace.");

    public static readonly Error InvalidPlateFormat =
        new("identity.plate_invalid_format", "license_plate format is invalid according to syntax rules.");
}
