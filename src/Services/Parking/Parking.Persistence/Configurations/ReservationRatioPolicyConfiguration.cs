using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Persistence.Configurations;

public class ReservationRatioPolicyConfiguration : IEntityTypeConfiguration<ReservationRatioPolicy>
{
    public void Configure(EntityTypeBuilder<ReservationRatioPolicy> builder)
    {
        builder.ToTable("reservation_ratio_policies");
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.LotId)
            .IsUnique()
            .HasDatabaseName("ux_reservation_ratio_policies_lot_id");

        builder.Property(p => p.ReservationRatioPercent)
            .IsRequired();

        builder.Property(p => p.UpdatedAtUtc)
            .IsRequired();
    }
}
