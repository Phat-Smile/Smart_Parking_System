using System.Text;
using System.Text.RegularExpressions;

namespace ParkingSystem.Identity.Application.Vehicles.Lookup;

public sealed partial class LicensePlateNormalizer : ILicensePlateNormalizer
{
    // [PROPOSED] Basic Vietnamese plate syntax: 2 digits + 1 letter + optional letter/digit + 4-5 digits.
    // Special series (NG, LD, military) are not covered until the team approves a full rule.
    [GeneratedRegex("^[0-9]{2}[A-Z][A-Z0-9]?[0-9]{4,5}$")]
    private static partial Regex BasicPlateRegex();

    public string Normalize(string? rawPlate)
    {
        if (string.IsNullOrWhiteSpace(rawPlate))
            return string.Empty;

        var builder = new StringBuilder(rawPlate.Length);
        foreach (var character in rawPlate)
        {
            if (char.IsWhiteSpace(character) || character is '-' or '.')
                continue;

            builder.Append(char.ToUpperInvariant(character));
        }

        return builder.ToString();
    }

    public bool IsValidFormat(string normalizedPlate)
    {
        if (string.IsNullOrWhiteSpace(normalizedPlate))
            return false;

        return BasicPlateRegex().IsMatch(normalizedPlate);
    }
}
