using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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
        builder.Property(session => session.EnteredAt)
            .HasColumnName("entered_at")
            .IsRequired();
        builder.Property(session => session.QrToken)
            .HasColumnName("qr_token")
            .HasMaxLength(ParkingSession.QrTokenMaxLength)
            .IsRequired();
        builder.Property(session => session.UserId).HasColumnName("user_id");
        builder.Property(session => session.VehicleId).HasColumnName("vehicle_id");
        builder.Property(session => session.BookingId).HasColumnName("booking_id");

        builder.HasIndex(session => session.QrToken)
            .IsUnique()
            .HasDatabaseName("ux_parking_sessions_qr_token");

        // At most one Active or Paid session per plate in a lot (ASSUMPTION, see OpenParkingSessionErrors).
        builder.HasIndex(session => new { session.LotId, session.PlateNumber })
            .IsUnique()
            .HasFilter(OpenStatusFilter)
            .HasDatabaseName(OpenPlateIndexName);
    }
}
