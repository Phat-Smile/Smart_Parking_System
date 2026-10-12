using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

internal sealed class ParkingSlotConfiguration : IEntityTypeConfiguration<ParkingSlot>
{
    private const int StateMaxLength = 32;

    public void Configure(EntityTypeBuilder<ParkingSlot> builder)
    {
        builder.ToTable("parking_slots", ParkingDbContext.DefaultSchema);
        builder.HasKey(slot => slot.Id).HasName("pk_parking_slots");

        builder.Property(slot => slot.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(slot => slot.LotId)
            .HasColumnName("lot_id")
            .IsRequired();
        builder.Property(slot => slot.ZoneId)
            .HasColumnName("zone_id")
            .IsRequired();
        builder.Property(slot => slot.SlotCode)
            .HasColumnName("slot_code")
            .HasMaxLength(ParkingSlot.SlotCodeMaxLength)
            .IsRequired();
        builder.Property(slot => slot.SensorState)
            .HasColumnName("sensor_state")
            .HasConversion<string>()
            .HasMaxLength(StateMaxLength);
        builder.Property(slot => slot.OverrideState)
            .HasColumnName("override_state")
            .HasConversion<string>()
            .HasMaxLength(StateMaxLength);
        builder.Property(slot => slot.DispatchState)
            .HasColumnName("dispatch_state")
            .HasConversion<string>()
            .HasMaxLength(StateMaxLength);
        builder.Property(slot => slot.AssignedSessionId)
            .HasColumnName("assigned_session_id");
        builder.Property(slot => slot.AssignedAt)
            .HasColumnName("assigned_at");
        builder.Property(slot => slot.EffectiveStatus)
            .HasColumnName("effective_status")
            .HasConversion<string>()
            .HasMaxLength(StateMaxLength)
            .IsRequired();
        builder.Property(slot => slot.RowVersion)
            .HasColumnName("row_version")
            .IsConcurrencyToken();

        builder.HasOne<ParkingLot>()
            .WithMany()
            .HasForeignKey(slot => slot.LotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_parking_slots_parking_lots_lot_id");
        builder.HasOne<Zone>()
            .WithMany()
            .HasForeignKey(slot => slot.ZoneId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_parking_slots_zones_zone_id");

        // ERD: a slot code is unique inside a lot.
        builder.HasIndex(slot => new { slot.LotId, slot.SlotCode })
            .IsUnique()
            .HasDatabaseName("ux_parking_slots_lot_slot_code");
        // ERD: a session holds at most one slot.
        builder.HasIndex(slot => slot.AssignedSessionId)
            .IsUnique()
            .HasFilter("assigned_session_id IS NOT NULL")
            .HasDatabaseName("ux_parking_slots_assigned_session_id");
        // Serves the Available count (contract 2) and the atomic designation.
        builder.HasIndex(slot => new { slot.LotId, slot.EffectiveStatus })
            .HasDatabaseName("ix_parking_slots_lot_id_effective_status");
        builder.HasIndex(slot => slot.ZoneId)
            .HasDatabaseName("ix_parking_slots_zone_id");
    }
}
