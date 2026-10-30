using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Parking.Domain.GateReadings;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.GateReadings;

// US-PARK-001: not in the team ERD yet (no table holds a reading waiting for staff; device_events has no status and needs
// a device_id). Decision 2026-10-29 (Thịnh): new table, ERD update requested in the PR.
internal sealed class GateReadingConfiguration : IEntityTypeConfiguration<GateReading>
{
    public void Configure(EntityTypeBuilder<GateReading> builder)
    {
        builder.ToTable("gate_readings", ParkingDbContext.DefaultSchema);
        builder.HasKey(reading => reading.Id).HasName("pk_gate_readings");

        builder.Property(reading => reading.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(reading => reading.LotId)
            .HasColumnName("lot_id")
            .IsRequired();
        builder.HasOne<ParkingLot>()
            .WithMany()
            .HasForeignKey(reading => reading.LotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_gate_readings_parking_lots_lot_id");
        builder.Property(reading => reading.RawPlate)
            .HasColumnName("raw_plate")
            .HasMaxLength(GateReading.RawPlateMaxLength)
            .IsRequired();
        builder.Property(reading => reading.PlateNumber)
            .HasColumnName("plate_number")
            .HasMaxLength(GateReading.RawPlateMaxLength)
            .IsRequired();
        builder.Property(reading => reading.VehicleType)
            .HasColumnName("vehicle_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        // 0.000 to 1.000.
        builder.Property(reading => reading.Confidence)
            .HasColumnName("confidence")
            .HasPrecision(4, 3)
            .IsRequired();
        // Concurrency token: confirming saves "WHERE decision = PendingStaff", so when two staff confirm the same reading at
        // once only the first save matches; the second fails and is reported as already confirmed (US-PARK-002).
        builder.Property(reading => reading.Decision)
            .HasColumnName("decision")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .IsConcurrencyToken();
        builder.Property(reading => reading.ReceivedAt)
            .HasColumnName("received_at")
            .IsRequired();
        builder.Property(reading => reading.RecordedBy)
            .HasColumnName("recorded_by")
            .IsRequired();
        builder.Property(reading => reading.ConfirmedBy).HasColumnName("confirmed_by");
        builder.Property(reading => reading.ConfirmedAt).HasColumnName("confirmed_at");

        // US-PARK-002 lists the readings of a lot that wait for staff, oldest first.
        builder.HasIndex(reading => new { reading.LotId, reading.Decision, reading.ReceivedAt })
            .HasDatabaseName("ix_gate_readings_lot_decision_received_at");
    }
}
