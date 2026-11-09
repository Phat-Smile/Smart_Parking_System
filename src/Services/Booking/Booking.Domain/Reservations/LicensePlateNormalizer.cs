using System.Text;

namespace ParkingSystem.Booking.Domain.Reservations;

/// <summary>
/// Normalizes vehicle license plates for canonical equivalence comparison,
/// faithfully following Identity service's normalization policy:
/// - Strips whitespace
/// - Strips punctuation delimiters ('-' and '.')
/// - Converts characters to uppercase invariant
/// </summary>
public static class LicensePlateNormalizer
{
    public static string Normalize(string? rawPlate)
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

    public static bool AreEquivalent(string? plateA, string? plateB) =>
        string.Equals(Normalize(plateA), Normalize(plateB), StringComparison.Ordinal);
}

