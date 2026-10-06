using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Booking.Domain.Reservations;

namespace ParkingSystem.Booking.Persistence.Reservations;

internal sealed class ReservationCapacityConfiguration : IEntityTypeConfiguration<ReservationCapacity>
{
    public void Configure(EntityTypeBuilder<ReservationCapacity> builder)
    {
        builder.ToTable("reservation_capacity", BookingDbContext.DefaultSchema);
        builder.HasKey(capacity => new { capacity.LotId, capacity.VehicleGroup })
            .HasName("pk_reservation_capacity");

        builder.Property(capacity => capacity.LotId)
            .HasColumnName("lot_id")
            .ValueGeneratedNever();

        builder.Property(capacity => capacity.VehicleGroup)
            .HasColumnName("vehicle_group")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(capacity => capacity.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}
