using System;

namespace ParkingSystem.Parking.Domain.Entities;

public class PricingPolicy
{
    public Guid Id { get; private set; }
    public Guid LotId { get; private set; }
    public string VehicleType { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public string BillingMode { get; private set; } = string.Empty;
    public int? BlockMinutes { get; private set; }
    public decimal Price { get; private set; }
    public decimal OvernightFee { get; private set; }
    public DateTime EffectiveFromUtc { get; private set; }

    private PricingPolicy() { }

    public static PricingPolicy Create(
        Guid lotId, 
        string vehicleType, 
        int version, 
        string billingMode, 
        int? blockMinutes, 
        decimal price, 
        decimal overnightFee)
    {
        return new PricingPolicy
        {
            Id = Guid.NewGuid(),
            LotId = lotId,
            VehicleType = vehicleType.ToUpperInvariant(),
            Version = version,
            BillingMode = billingMode,
            BlockMinutes = blockMinutes,
            Price = price,
            OvernightFee = overnightFee,
            EffectiveFromUtc = DateTime.UtcNow
        };
    }

    public bool HasSameValues(string billingMode, int? blockMinutes, decimal price, decimal overnightFee)
    {
        return BillingMode == billingMode &&
               BlockMinutes == blockMinutes &&
               Price == price &&
               OvernightFee == overnightFee;
    }
}
