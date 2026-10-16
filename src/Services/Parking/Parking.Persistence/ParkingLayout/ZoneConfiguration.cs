using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Parking.Domain.ParkingLayout;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

internal sealed class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> builder)
    {
        builder.ToTable("zones", ParkingDbContext.DefaultSchema, table =>
        {
            table.HasCheckConstraint("ck_zones_capacity_not_negative", "capacity >= 0");
            // BR-07.b: the motorbike count never goes below 0.
            table.HasCheckConstraint("ck_zones_current_vehicle_count_not_negative", "current_vehicle_count >= 0");
        });
        builder.HasKey(zone => zone.Id).HasName("pk_zones");

        builder.Property(zone => zone.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(zone => zone.FloorId)
            .HasColumnName("floor_id")
            .IsRequired();
        builder.Property(zone => zone.Code)
            .HasColumnName("code")
            .HasMaxLength(Zone.CodeMaxLength)
            .IsRequired();
        builder.Property(zone => zone.VehicleGroup)
            .HasColumnName("vehicle_group")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(zone => zone.Capacity)
            .HasColumnName("capacity")
            .IsRequired();
        builder.Property(zone => zone.CurrentVehicleCount)
            .HasColumnName("current_vehicle_count")
            .IsRequired();
        builder.Property(zone => zone.HasSlotSensor)
            .HasColumnName("has_slot_sensor")
            .IsRequired();

        builder.HasOne<Floor>()
            .WithMany()
            .HasForeignKey(zone => zone.FloorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_zones_floors_floor_id");
        builder.HasIndex(zone => zone.FloorId)
            .HasDatabaseName("ix_zones_floor_id");
    }
}
