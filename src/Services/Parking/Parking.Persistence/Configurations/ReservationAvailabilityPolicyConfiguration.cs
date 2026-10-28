using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Persistence.Configurations;

public class ReservationAvailabilityPolicyConfiguration : IEntityTypeConfiguration<ReservationAvailabilityPolicy>
{
    public void Configure(EntityTypeBuilder<ReservationAvailabilityPolicy> builder)
    {
        builder.ToTable("reservation_availability_policies");
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.LotId)
            .IsUnique()
            .HasDatabaseName("ux_reservation_availability_policies_lot_id");

        builder.Property(p => p.ReservationEnabled)
            .IsRequired();

        builder.Property(p => p.UpdatedAtUtc)
            .IsRequired();
    }
}
