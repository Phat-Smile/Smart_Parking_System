namespace ParkingSystem.Parking.Application.ParkingLots.OwnerLocation;

public static class OwnerLotLocationRules
{
    public static Dictionary<string, string[]> ValidateConfirmation(
        string? address, decimal? latitude, decimal? longitude, bool confirmedOnMap)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(address) || address.Trim().Length > 500)
            errors["address"] = ["A nonempty address of at most 500 characters is required."];
        if (latitude is null or < -90m or > 90m)
            errors["latitude"] = ["Latitude must be between -90 and 90."];
        if (longitude is null or < -180m or > 180m)
            errors["longitude"] = ["Longitude must be between -180 and 180."];
        if (latitude == 0m && longitude == 0m)
            errors["location"] = ["The default (0, 0) location cannot be used as the parking entrance."];
        if (!confirmedOnMap)
            errors["confirmedOnMap"] = ["The owner must explicitly confirm the entrance pin on the map."];
        return errors;
    }
}
