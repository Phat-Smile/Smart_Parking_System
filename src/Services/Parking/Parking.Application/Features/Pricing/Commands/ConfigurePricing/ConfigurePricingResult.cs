namespace ParkingSystem.Parking.Application.Features.Pricing.Commands.ConfigurePricing;

public class ConfigurePricingResult
{
    public int Version { get; set; }
    public bool IsNewVersionCreated { get; set; }

    public ConfigurePricingResult(int version, bool isNewVersionCreated)
    {
        Version = version;
        IsNewVersionCreated = isNewVersionCreated;
    }
}
