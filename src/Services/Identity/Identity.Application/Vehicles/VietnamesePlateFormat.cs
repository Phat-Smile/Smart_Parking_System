using System.Text.RegularExpressions;
using ParkingSystem.Identity.Domain.Vehicles;

namespace ParkingSystem.Identity.Application.Vehicles;

/// <summary>Syntax for civilian car and motorbike plates, including older four-digit serials.
/// This cannot establish registration validity or ownership.</summary>
public static partial class VietnamesePlateFormat
{
    [GeneratedRegex("^[1-9][0-9][A-Z]{1,2}[0-9]{4,5}$", RegexOptions.CultureInvariant)]
    private static partial Regex CarPattern();

    [GeneratedRegex("^[1-9][0-9][A-Z][0-9][0-9]{4,5}$", RegexOptions.CultureInvariant)]
    private static partial Regex MotorbikePattern();

    public static bool IsValid(string? canonicalPlate, VehicleDomainType type) =>
        canonicalPlate is not null && (type switch
        {
            VehicleDomainType.Car => CarPattern().IsMatch(canonicalPlate),
            VehicleDomainType.Motorbike => MotorbikePattern().IsMatch(canonicalPlate),
            VehicleDomainType.Bicycle => canonicalPlate.Length == 0,
            _ => false,
        });
}
