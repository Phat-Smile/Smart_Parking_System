using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Booking.Domain.Reservations;

namespace ParkingSystem.Booking.Persistence.Reservations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations", BookingDbContext.DefaultSchema);
        builder.HasKey(reservation => reservation.Id).HasName("pk_reservations");

        builder.Property(reservation => reservation.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(reservation => reservation.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(reservation => reservation.VehicleId)
            .HasColumnName("vehicle_id")
            .IsRequired();

        builder.Property(reservation => reservation.PlateNumber)
            .HasColumnName("plate_number")
            .HasMaxLength(Reservation.PlateNumberMaxLength)
            .IsRequired();

        builder.Property(reservation => reservation.LotId)
            .HasColumnName("lot_id")
            .IsRequired();

        builder.Property(reservation => reservation.VehicleGroup)
            .HasColumnName("vehicle_group")
            .HasColumnType("booking.vehicle_group")
            .IsRequired();

        builder.Property(reservation => reservation.ReservationType)
            .HasColumnName("reservation_type")
            .HasColumnType("booking.reservation_type")
            .IsRequired();

        builder.Property(reservation => reservation.Status)
            .HasColumnName("status")
            .HasColumnType("booking.reservation_status")
            .IsRequired();

        builder.Property(reservation => reservation.AppointmentTime)
            .HasColumnName("appointment_time")
            .IsRequired();

        builder.Property(reservation => reservation.DurationMinutes)
            .HasColumnName("duration_minutes");

        builder.Property(reservation => reservation.WindowStartAt)
            .HasColumnName("window_start_at")
            .IsRequired();

        builder.Property(reservation => reservation.WindowEndAt)
            .HasColumnName("window_end_at")
            .IsRequired();

        builder.Property(reservation => reservation.HoldEndAt)
            .HasColumnName("hold_end_at")
            .IsRequired();

        builder.Property(reservation => reservation.TempHoldExpiresAt)
            .HasColumnName("temp_hold_expires_at");

        builder.Property(reservation => reservation.PrepaidAmount)
            .HasColumnName("prepaid_amount")
            .HasPrecision(12, 0)
            .IsRequired();

        builder.Property(reservation => reservation.PriceSnapshot)
            .HasColumnName("price_snapshot")
            .HasColumnType("jsonb");

        builder.Property(reservation => reservation.ConfirmedAt)
            .HasColumnName("confirmed_at");

        builder.Property(reservation => reservation.UsedAt)
            .HasColumnName("used_at");

        builder.Property(reservation => reservation.SessionId)
            .HasColumnName("session_id");

        builder.Property(reservation => reservation.CancelledAt)
            .HasColumnName("cancelled_at");

        builder.Property(reservation => reservation.CancelReason)
            .HasColumnName("cancel_reason")
            .HasColumnType("booking.cancel_reason");

        builder.Property(reservation => reservation.PrepaidOutcome)
            .HasColumnName("prepaid_outcome")
            .HasColumnType("booking.prepaid_outcome");

        builder.Property(reservation => reservation.IsNoShow)
            .HasColumnName("is_no_show")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(reservation => reservation.RowVersion)
            .HasColumnName("row_version")
            .HasDefaultValue(0)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(reservation => reservation.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(reservation => reservation.UserId)
            .HasDatabaseName("ix_reservations_user_id");

        builder.HasIndex(reservation => new { reservation.LotId, reservation.Status })
            .HasDatabaseName("ix_reservations_lot_id_status");

        builder.HasIndex(reservation => new { reservation.Status, reservation.TempHoldExpiresAt })
            .HasDatabaseName("ix_reservations_status_temp_hold_expires_at");

        builder.HasIndex(reservation => new { reservation.LotId, reservation.VehicleGroup, reservation.Status, reservation.WindowStartAt, reservation.WindowEndAt })
            .HasDatabaseName("ix_reservations_capacity_check");

        builder.HasIndex(reservation => new { reservation.LotId, reservation.VehicleGroup, reservation.Status, reservation.WindowStartAt, reservation.HoldEndAt })
            .HasDatabaseName("ix_reservations_active_hold");

        builder.HasIndex(reservation => new { reservation.PlateNumber, reservation.Status, reservation.WindowStartAt, reservation.WindowEndAt })
            .HasDatabaseName("ix_reservations_plate_overlap");
    }
}
