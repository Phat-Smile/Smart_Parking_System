using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

internal sealed class FloorConfiguration : IEntityTypeConfiguration<Floor>
{
    public void Configure(EntityTypeBuilder<Floor> builder)
    {
        builder.ToTable("floors", ParkingDbContext.DefaultSchema);
        builder.HasKey(floor => floor.Id).HasName("pk_floors");

        builder.Property(floor => floor.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(floor => floor.LotId)
            .HasColumnName("lot_id")
            .IsRequired();
        builder.Property(floor => floor.Name)
            .HasColumnName("name")
            .HasMaxLength(Floor.NameMaxLength)
            .IsRequired();

        builder.HasOne<ParkingLot>()
            .WithMany()
            .HasForeignKey(floor => floor.LotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_floors_parking_lots_lot_id");
        builder.HasIndex(floor => floor.LotId)
            .HasDatabaseName("ix_floors_lot_id");
    }
}
