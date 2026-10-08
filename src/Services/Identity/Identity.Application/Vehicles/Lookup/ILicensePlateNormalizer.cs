namespace ParkingSystem.Identity.Application.Vehicles.Lookup;

public interface ILicensePlateNormalizer
{
    string Normalize(string? rawPlate);
    bool IsValidFormat(string normalizedPlate);
}
