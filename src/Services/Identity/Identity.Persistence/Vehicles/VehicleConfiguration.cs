using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Vehicles;

namespace ParkingSystem.Identity.Persistence.Vehicles;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public const string PlateIndexName = "ix_vehicles_license_plate";
    public const string AccountIdIndexName = "ix_vehicles_account_id";
    public const string DefaultVehicleIndexName = "ix_vehicles_account_id_is_default";

    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles", IdentityDbContext.DefaultSchema);

        builder.HasKey(v => v.Id).HasName("pk_vehicles");
        builder.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(v => v.AccountId).HasColumnName("account_id").IsRequired();

        builder.Property(v => v.LicensePlate)
            .HasColumnName("license_plate")
            .HasMaxLength(Vehicle.MaxLicensePlateLength);

        builder.HasIndex(v => v.LicensePlate)
            .IsUnique()
            .HasDatabaseName(PlateIndexName);

        builder.HasIndex(v => v.AccountId)
            .HasDatabaseName(AccountIdIndexName);

        builder.Property(v => v.VehicleType)
            .HasColumnName("vehicle_type")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(v => v.NickName)
            .HasColumnName("nick_name")
            .HasMaxLength(Vehicle.MaxNickNameLength);

        builder.Property(v => v.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(v => v.IsDefault)
            .HasColumnName("is_default")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(v => v.Brand).HasColumnName("brand").HasMaxLength(100);
        builder.Property(v => v.Model).HasColumnName("model").HasMaxLength(100);
        builder.Property(v => v.MotorbikeKind).HasColumnName("motorbike_kind").HasMaxLength(50);
        builder.Property(v => v.Seats).HasColumnName("seats");
        builder.Property(v => v.FuelType).HasColumnName("fuel_type").HasMaxLength(30);
        builder.Property(v => v.Color).HasColumnName("color").HasMaxLength(50);
        builder.Property(v => v.ManufacturingYear).HasColumnName("manufacturing_year");
        builder.Property(v => v.Size).HasColumnName("size").HasMaxLength(20);
        builder.Property(v => v.PhotoUrl).HasColumnName("photo_url").HasMaxLength(500);

        builder.HasIndex(v => new { v.AccountId, v.IsDefault })
            .IsUnique()
            .HasFilter("\"is_default\" = TRUE")
            .HasDatabaseName(DefaultVehicleIndexName);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(v => v.AccountId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_vehicles_accounts_account_id");
    }
}
