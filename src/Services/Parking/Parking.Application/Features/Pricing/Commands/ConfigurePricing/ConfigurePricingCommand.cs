using System;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.Features.Pricing.Commands.ConfigurePricing;

public class ConfigurePricingCommand : IRequest<Result<ConfigurePricingResult>>
{
    public Guid LotId { get; set; }
    public string VehicleType { get; set; } = string.Empty;
    public string BillingMode { get; set; } = string.Empty;
    public int? BlockMinutes { get; set; }
    public decimal Price { get; set; }
    public decimal OvernightFee { get; set; }
    public int? ExpectedVersion { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid CallerId { get; set; }
}
