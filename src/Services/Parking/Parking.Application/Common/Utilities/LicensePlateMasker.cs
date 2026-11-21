namespace ParkingSystem.Parking.Application.Common.Utilities;

/// <summary>
/// Masks license plate numbers according to NFR-15 (e.g. "51F-***.89")
/// to protect driver PII on public endpoints and events.
/// </summary>
public static class LicensePlateMasker
{
    public static string MaskPlateNumber(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
            return "***-***.**";

        var trimmed = plate.Trim().ToUpperInvariant();
        var alphanumeric = new string(trimmed.Where(char.IsLetterOrDigit).ToArray());

        if (alphanumeric.Length < 5)
            return "***-***.**";

        var prefix = alphanumeric[..3];
        var suffix = alphanumeric[^2..];

        return $"{prefix}-***.{suffix}";
    }
}
