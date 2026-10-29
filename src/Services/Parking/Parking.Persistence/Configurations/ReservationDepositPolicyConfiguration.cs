using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Persistence.Configurations;

public class ReservationDepositPolicyConfiguration : IEntityTypeConfiguration<ReservationDepositPolicy>
{
    public void Configure(EntityTypeBuilder<ReservationDepositPolicy> builder)
    {
        builder.ToTable("reservation_deposit_policies");
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.LotId)
            .IsUnique()
            .HasDatabaseName("ux_reservation_deposit_policies_lot_id");

        builder.Property(p => p.HoldDepositVnd)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(p => p.UpdatedAtUtc)
            .IsRequired();
    }
}
