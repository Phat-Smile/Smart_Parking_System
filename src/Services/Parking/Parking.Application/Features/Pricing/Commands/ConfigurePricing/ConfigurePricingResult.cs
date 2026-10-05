namespace ParkingSystem.Parking.Application.Features.Pricing.Commands.ConfigurePricing;

public class ConfigurePricingResult
{
    public int Version { get; set; }
    public bool IsIdentical { get; set; }

    public ConfigurePricingResult(int version, bool isIdentical)
    {
        Version = version;
        IsIdentical = isIdentical;
    }
}
