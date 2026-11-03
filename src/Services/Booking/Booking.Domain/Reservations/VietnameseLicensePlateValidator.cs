using System.Text.RegularExpressions;

namespace ParkingSystem.Booking.Domain.Reservations;

/// <summary>
/// Validates Vietnamese civilian vehicle license plate syntax.
/// Reuses <see cref="LicensePlateNormalizer"/> for canonicalization (stripping whitespace, hyphens, dots).
///
/// Syntax specification:
/// - 2 digits (province code: 11-99)
/// - 1 series letter [A-Z]
/// - Optional 1 letter or digit [A-Z0-9] (supports 2-letter series such as 51LD, 29NN, or 4-digit sub-series)
/// - 4 to 5 digits (serial registration number)
///
/// Important Architectural Boundaries:
/// - Syntax validation only ensures structural compliance with standard Vietnamese plates.
/// - This validator does NOT determine vehicle group (e.g. Car vs Motorbike).
///   Identity Service is the authoritative owner for registered vehicle attributes and VehicleType.
/// </summary>
public static partial class VietnameseLicensePlateValidator
{
    [GeneratedRegex("^[0-9]{2}[A-Z][A-Z0-9]?[0-9]{4,5}$", RegexOptions.CultureInvariant)]
    private static partial Regex BasicPlateRegex();

    /// <summary>
    /// Checks whether the raw license plate conforms to valid Vietnamese plate syntax after normalization.
    /// </summary>
    public static bool IsValid(string? rawPlate)
    {
        if (string.IsNullOrWhiteSpace(rawPlate))
            return false;

        var normalized = LicensePlateNormalizer.Normalize(rawPlate);
        return IsNormalizedValid(normalized);
    }

    /// <summary>
    /// Checks whether an already canonicalized/normalized plate conforms to valid syntax.
    /// </summary>
    public static bool IsNormalizedValid(string? normalizedPlate)
    {
        if (string.IsNullOrWhiteSpace(normalizedPlate))
            return false;

        return BasicPlateRegex().IsMatch(normalizedPlate);
    }

    /// <summary>
    /// Attempts to normalize and validate the license plate.
    /// </summary>
    /// <param name="rawPlate">Input raw plate string from user or client.</param>
    /// <param name="canonicalPlate">Outputs the normalized uppercase canonical plate if valid; otherwise empty string.</param>
    /// <returns>True if the plate is structurally valid; otherwise false.</returns>
    public static bool TryNormalizeAndValidate(string? rawPlate, out string canonicalPlate)
    {
        if (string.IsNullOrWhiteSpace(rawPlate))
        {
            canonicalPlate = string.Empty;
            return false;
        }

        var normalized = LicensePlateNormalizer.Normalize(rawPlate);
        if (IsNormalizedValid(normalized))
        {
            canonicalPlate = normalized;
            return true;
        }

        canonicalPlate = string.Empty;
        return false;
    }
}

