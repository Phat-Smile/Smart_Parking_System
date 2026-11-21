using ParkingSystem.Parking.Application.Features.Pricing.Commands.ConfigurePricing;

namespace ParkingSystem.Parking.WebAPI.DTOs;

public class ConfigurePricingRequest
{
    public string BillingMode { get; set; } = string.Empty;
    public int? BlockMinutes { get; set; }
    public decimal Price { get; set; }
    public decimal OvernightFee { get; set; }
    public int? ExpectedVersion { get; set; }
    public string Reason { get; set; } = string.Empty;

    public ConfigurePricingCommand ToCommand(Guid lotId, string vehicleType, Guid callerId)
    {
        return new ConfigurePricingCommand
        {
            LotId = lotId,
            VehicleType = vehicleType,
            CallerId = callerId,
            BillingMode = BillingMode,
            BlockMinutes = BlockMinutes,
            Price = Price,
            OvernightFee = OvernightFee,
            ExpectedVersion = ExpectedVersion,
            Reason = Reason
        };
    }
}

// Sprint 5 BE-PK03 route contract: one lot and an explicit vehicle type in the body.
// This is an alias over the existing per-vehicle pricing handler; it does not
// introduce progressive block calculations without an approved Payment contract.
public sealed class ConfigureLotPricingRequest : ConfigurePricingRequest
{
    public string VehicleType { get; set; } = string.Empty;

    public ConfigurePricingCommand ToCommand(Guid lotId, Guid callerId) =>
        base.ToCommand(lotId, VehicleType, callerId);
}
