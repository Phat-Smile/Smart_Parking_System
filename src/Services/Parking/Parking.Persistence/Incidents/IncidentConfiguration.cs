using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Parking.Domain.Incidents;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Persistence.Incidents;

// Team ERD parking.incidents. device_id has no FK yet: parking.devices is not built (Sprint 3, MQTT devices).
internal sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("incidents", ParkingDbContext.DefaultSchema);
        builder.HasKey(incident => incident.Id).HasName("pk_incidents");

        builder.Property(incident => incident.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(incident => incident.LotId)
            .HasColumnName("lot_id")
            .IsRequired();
        builder.HasOne<ParkingLot>()
            .WithMany()
            .HasForeignKey(incident => incident.LotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_incidents_parking_lots_lot_id");
        builder.Property(incident => incident.IncidentType)
            .HasColumnName("incident_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        // Concurrency token: resolving saves "WHERE status = Open", so when two staff resolve the same incident at once only
        // the first save matches; the second fails and is reported as already handled (US-PARK-002).
        builder.Property(incident => incident.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .IsConcurrencyToken();
        builder.Property(incident => incident.SessionId).HasColumnName("session_id");
        builder.HasOne<ParkingSession>()
            .WithMany()
            .HasForeignKey(incident => incident.SessionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_incidents_parking_sessions_session_id");
        // ERD: ref booking.reservations, no FK (another service).
        builder.Property(incident => incident.ReservationId).HasColumnName("reservation_id");
        builder.Property(incident => incident.SlotId).HasColumnName("slot_id");
        builder.HasOne<ParkingSlot>()
            .WithMany()
            .HasForeignKey(incident => incident.SlotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_incidents_parking_slots_slot_id");
        builder.Property(incident => incident.DeviceId)
            .HasColumnName("device_id")
            .HasMaxLength(Incident.DeviceIdMaxLength);
        builder.Property(incident => incident.ObservedPlate)
            .HasColumnName("observed_plate")
            .HasMaxLength(ParkingSession.PlateNumberMaxLength);
        builder.Property(incident => incident.PlateConfidence)
            .HasColumnName("plate_confidence")
            .HasPrecision(3, Incident.ConfidenceDecimals);
        builder.Property(incident => incident.ObservedVehicleType)
            .HasColumnName("observed_vehicle_type")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(incident => incident.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();
        builder.Property(incident => incident.ResolvedBy).HasColumnName("resolved_by");
        builder.Property(incident => incident.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(incident => incident.ResolutionReason)
            .HasColumnName("resolution_reason")
            .HasMaxLength(Incident.ResolutionReasonMaxLength);

        // US-PARK-002 lists the open incidents of a lot by type, oldest first.
        builder.HasIndex(incident => new { incident.LotId, incident.Status, incident.IncidentType, incident.OccurredAt })
            .HasDatabaseName("ix_incidents_lot_status_type_occurred_at");
        builder.HasIndex(incident => incident.SessionId).HasDatabaseName("ix_incidents_session_id");
        builder.HasIndex(incident => incident.SlotId).HasDatabaseName("ix_incidents_slot_id");
    }
}
