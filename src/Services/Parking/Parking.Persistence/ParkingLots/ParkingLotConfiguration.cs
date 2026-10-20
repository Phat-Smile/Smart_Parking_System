using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class ParkingLotConfiguration : IEntityTypeConfiguration<ParkingLot>
{
    public void Configure(EntityTypeBuilder<ParkingLot> builder)
    {
        builder.ToTable("parking_lots", ParkingDbContext.DefaultSchema);
        builder.HasKey(parkingLot => parkingLot.Id).HasName("pk_parking_lots");

        builder.Property(parkingLot => parkingLot.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
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
        builder.Property(parkingLot => parkingLot.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(parkingLot => parkingLot.ApprovalStatus)
            .HasColumnName("approval_status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(parkingLot => new { parkingLot.ApprovalStatus, parkingLot.Name })
            .HasDatabaseName("ix_parking_lots_approval_status_name");
    }
}
