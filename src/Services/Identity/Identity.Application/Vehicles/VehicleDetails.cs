using FluentValidation;
using ParkingSystem.Identity.Domain.Vehicles;

namespace ParkingSystem.Identity.Application.Vehicles;

public sealed record VehicleDetails(
    string? Brand, string? Model, string? MotorbikeKind, int? Seats,
    string? FuelType, string? Color, int? ManufacturingYear, string? Size, string? PhotoUrl);

public static class VehicleDetailsValidation
{
    public static void AddRules<T>(AbstractValidator<T> validator, Func<T, VehicleDetails?> details)
    {
        validator.RuleFor(x => details(x)!.Brand).MaximumLength(100).When(x => details(x) is not null);
        validator.RuleFor(x => details(x)!.Model).MaximumLength(100).When(x => details(x) is not null);
        validator.RuleFor(x => details(x)!.MotorbikeKind).MaximumLength(50).When(x => details(x) is not null);
        validator.RuleFor(x => details(x)!.FuelType).MaximumLength(30).When(x => details(x) is not null);
        validator.RuleFor(x => details(x)!.Color).MaximumLength(50).When(x => details(x) is not null);
        validator.RuleFor(x => details(x)!.Size).Must(s => s is null or "standard" or "large").When(x => details(x) is not null);
        validator.RuleFor(x => details(x)!.Seats).InclusiveBetween(1, 50).When(x => details(x)?.Seats is not null);
        validator.RuleFor(x => details(x)!.ManufacturingYear).InclusiveBetween(1886, DateTime.UtcNow.Year + 1)
            .When(x => details(x)?.ManufacturingYear is not null);
        validator.RuleFor(x => details(x)!.PhotoUrl).MaximumLength(500)
            .Must(url => url is null || IsCustomerMediaUrl(url) || IsSystemVehicleImageUrl(url) ||
                (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
            .When(x => details(x) is not null);
    }

    public static bool IsCustomerMediaUrl(string? url) => url is not null &&
        url.StartsWith("/api/identity/accounts/me/media/", StringComparison.Ordinal) &&
        Guid.TryParse(url["/api/identity/accounts/me/media/".Length..], out _);

    public static bool IsSystemVehicleImageUrl(string? url) => url is
        "/images/parkwise/vehicle-bicycle-default.svg" or
        "/images/parkwise/vehicle-electric-motorbike-default.svg" or
        "/images/parkwise/vehicle-motorbike-cutout.png" or
        "/images/parkwise/vehicle-sedan-cutout.png" or
        "/images/parkwise/vehicle-suv.png";

    public static bool IsValidBicycleImageUrl(string? url) =>
        IsCustomerMediaUrl(url) || url == "/images/parkwise/vehicle-bicycle-default.svg";

    public static void Apply(Vehicle vehicle, string? nickName, VehicleDetails? details)
    {
        if (details is null) return;
        vehicle.UpdateDetails(nickName, details.Brand, details.Model, details.MotorbikeKind,
            details.Seats, details.FuelType, details.Color, details.ManufacturingYear, details.Size, details.PhotoUrl);
    }
}
