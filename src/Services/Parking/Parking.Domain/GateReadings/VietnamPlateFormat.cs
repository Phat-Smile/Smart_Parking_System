using System.Text;
using System.Text.RegularExpressions;

namespace ParkingSystem.Parking.Domain.GateReadings;

/// <summary>
/// BR-05.a "biển đúng định dạng Việt Nam". Same rule as Identity's LicensePlateNormalizer (copied, not shared: Identity is
/// another service and team's part); keep the two in step. [PROPOSED] in Identity too: basic series only, special series
/// (NG, LD, military) are not covered until the team approves a full rule, so those go to staff.
/// </summary>
public static partial class VietnamPlateFormat
{
    // 2 digits + 1 letter + optional letter/digit + 4-5 digits, after removing spaces, '-' and '.'.
    [GeneratedRegex("^[0-9]{2}[A-Z][A-Z0-9]?[0-9]{4,5}$")]
    private static partial Regex BasicPlateRegex();

    public static bool IsValid(string? plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
            return false;

        var compact = new StringBuilder(plate.Length);
        foreach (var character in plate)
        {
            if (char.IsWhiteSpace(character) || character is '-' or '.')
                continue;
            compact.Append(char.ToUpperInvariant(character));
        }

        return BasicPlateRegex().IsMatch(compact.ToString());
    }
}
