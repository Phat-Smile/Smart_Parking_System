using System.Text.RegularExpressions;

namespace ParkingSystem.Identity.Application.Vehicles.Lookup;

public sealed partial class LicensePlateNormalizer : ILicensePlateNormalizer
{
    // Basic Vietnamese license plate syntax: 2 digits + 1-2 alphanumeric + 4-5 digits
    [GeneratedRegex("^[0-9]{2}[A-Z0-9]{1,2}[0-9]{4,5}$")]
    private static partial Regex BasicPlateRegex();

    public string Normalize(string? rawPlate)
    {
        if (string.IsNullOrWhiteSpace(rawPlate))
            return string.Empty;

        var upper = rawPlate.Trim().ToUpperInvariant();
        return upper.Replace("-", "").Replace(".", "").Replace(" ", "");
    }

    public bool IsValidFormat(string normalizedPlate)
    {
        if (string.IsNullOrWhiteSpace(normalizedPlate))
            return false;

        return BasicPlateRegex().IsMatch(normalizedPlate);
    }
}
