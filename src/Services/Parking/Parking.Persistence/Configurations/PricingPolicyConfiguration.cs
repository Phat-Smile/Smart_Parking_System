using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Persistence.Configurations;

public class PricingPolicyConfiguration : IEntityTypeConfiguration<PricingPolicy>
{
    public void Configure(EntityTypeBuilder<PricingPolicy> builder)
    {
        builder.ToTable("pricing_policies");
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => new { p.LotId, p.VehicleType, p.Version })
               .IsUnique()
               .HasDatabaseName("ux_pricing_policies_lot_vehicle_version");

        builder.Property(p => p.VehicleType).IsRequired().HasMaxLength(50);
        builder.Property(p => p.BillingMode).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Price).HasColumnType("decimal(18,2)");
        builder.Property(p => p.OvernightFee).HasColumnType("decimal(18,2)");
    }
}
