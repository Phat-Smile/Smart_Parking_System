using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class ParkingLotConfiguration : IEntityTypeConfiguration<ParkingLot>
{
    internal const string OwnerNameUniqueIndex = "ux_parking_lots_owner_user_id_name";
    public void Configure(EntityTypeBuilder<ParkingLot> builder)
    {
        builder.ToTable("parking_lots", ParkingDbContext.DefaultSchema);
        builder.HasKey(parkingLot => parkingLot.Id).HasName("pk_parking_lots");

        builder.Property(parkingLot => parkingLot.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        // Optional only for legacy catalog rows and S1 seeded lots.
        builder.Property(parkingLot => parkingLot.OwnerUserId)
            .HasColumnName("owner_user_id");

        builder.Property(parkingLot => parkingLot.Name)
            .HasColumnName("name")
            .IsRequired();
        builder.Property(parkingLot => parkingLot.Address)
            .HasColumnName("address")
            .IsRequired();
        builder.Property(parkingLot => parkingLot.Latitude)
            .HasColumnName("latitude")
            .HasPrecision(9, 6)
            .IsRequired();
        builder.Property(parkingLot => parkingLot.Longitude)
            .HasColumnName("longitude")
            .HasPrecision(9, 6)
            .IsRequired();
        builder.Property(parkingLot => parkingLot.OpeningHoursJson)
            .HasColumnName("opening_hours")
            .HasColumnType("jsonb");
        builder.Property(parkingLot => parkingLot.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(parkingLot => parkingLot.ApprovalStatus)
            .HasColumnName("approval_status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            // Optimistic concurrency: a second reviewer cannot overwrite the first decision.
            .IsConcurrencyToken();
        builder.Property(parkingLot => parkingLot.RejectionReason)
            .HasColumnName("rejection_reason")
            .HasMaxLength(ParkingLot.RejectionReasonMaxLength);

        builder.HasIndex(parkingLot => parkingLot.OwnerUserId)
            .HasDatabaseName("ix_parking_lots_owner_user_id");

        // Nullable owner IDs on legacy S1 rows do not conflict in PostgreSQL.
        builder.HasIndex(parkingLot => new { parkingLot.OwnerUserId, parkingLot.Name })
            .IsUnique()
            .HasDatabaseName(OwnerNameUniqueIndex);

        builder.HasIndex(parkingLot => new { parkingLot.ApprovalStatus, parkingLot.Name })
            .HasDatabaseName("ix_parking_lots_approval_status_name");
    }
}
