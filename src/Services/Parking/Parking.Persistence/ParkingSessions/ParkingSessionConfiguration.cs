using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Persistence.ParkingSessions;

internal sealed class ParkingSessionConfiguration : IEntityTypeConfiguration<ParkingSession>
{
    internal const string OpenPlateIndexName = "ux_parking_sessions_lot_plate_open";
    private const string StatusColumn = "status";

    // Built from the domain list so the index and the repository query cannot drift apart.
    private static readonly string OpenStatusFilter =
        $"{StatusColumn} IN ({string.Join(", ", ParkingSession.OpenStatuses.Select(status => $"'{status}'"))})";

    public void Configure(EntityTypeBuilder<ParkingSession> builder)
    {
        builder.ToTable("parking_sessions", ParkingDbContext.DefaultSchema);
        builder.HasKey(session => session.Id).HasName("pk_parking_sessions");

        builder.Property(session => session.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(session => session.LotId)
            .HasColumnName("lot_id")
            .IsRequired();
        // ERD: lot_id references parking_lots. Restrict: a lot with sessions cannot be deleted.
        builder.HasOne<ParkingLot>()
            .WithMany()
            .HasForeignKey(session => session.LotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_parking_sessions_parking_lots_lot_id");
        builder.Property(session => session.PlateNumber)
            .HasColumnName("plate_number")
            .HasMaxLength(ParkingSession.PlateNumberMaxLength)
            .IsRequired();
        builder.Property(session => session.VehicleType)
            .HasColumnName("vehicle_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(session => session.Status)
            .HasColumnName(StatusColumn)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(session => session.EntryTime)
            .HasColumnName("entry_time")
            .IsRequired();
        builder.Property(session => session.QrToken)
            .HasColumnName("qr_token")
            .HasMaxLength(ParkingSession.QrTokenMaxLength)
            .IsRequired();
        builder.Property(session => session.LastPaidAt).HasColumnName("last_paid_at");
        builder.Property(session => session.ExitTime).HasColumnName("exit_time");
        builder.Property(session => session.ExitArrivedAt).HasColumnName("exit_arrived_at");
        builder.Property(session => session.HasExitWindowExtension).HasColumnName("has_exit_window_extension");
        builder.Property(session => session.ExitWindowExtendedAt).HasColumnName("exit_window_extended_at");
        builder.Property(session => session.ExitWindowExtensionReason)
            .HasColumnName("exit_window_extension_reason")
            .HasMaxLength(ParkingSession.StaffActionReasonMaxLength);
        builder.Property(session => session.UserId).HasColumnName("user_id");
        builder.Property(session => session.VehicleId).HasColumnName("vehicle_id");
        builder.Property(session => session.ReservationId).HasColumnName("reservation_id");
        // ERD: zone_id NOT NULL; kept nullable until car sessions get their zone in US-PARK-004/007 (team decision 2026-10-16).
        builder.Property(session => session.ZoneId).HasColumnName("zone_id");
        builder.HasOne<Zone>()
            .WithMany()
            .HasForeignKey(session => session.ZoneId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_parking_sessions_zones_zone_id");
        builder.HasIndex(session => session.ZoneId).HasDatabaseName("ix_parking_sessions_zone_id");
        builder.Property(session => session.CancelledAt).HasColumnName("cancelled_at");
        builder.Property(session => session.CancelledBy).HasColumnName("cancelled_by");
        builder.Property(session => session.CancelReason)
            .HasColumnName("cancel_reason")
            .HasMaxLength(ParkingSession.CancelReasonMaxLength);
        builder.Ignore(session => session.CustomerType); // derived, not stored
        builder.Ignore(session => session.ExitWindowEndsAt); // derived, not stored

        builder.HasIndex(session => session.QrToken)
            .IsUnique()
            .HasDatabaseName("ux_parking_sessions_qr_token");

        // US-PARK-009: a customer lists their own open sessions.
        builder.HasIndex(session => session.UserId)
            .HasFilter("user_id IS NOT NULL")
            .HasDatabaseName("ix_parking_sessions_user_id");

        // At most one Active or Paid session per plate in a lot (ASSUMPTION, see OpenParkingSessionErrors).
        builder.HasIndex(session => new { session.LotId, session.PlateNumber })
            .IsUnique()
            .HasFilter(OpenStatusFilter)
            .HasDatabaseName(OpenPlateIndexName);
    }
}
