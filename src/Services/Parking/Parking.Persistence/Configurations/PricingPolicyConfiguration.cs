using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Parking.Domain.Entities;

namespace ParkingSystem.Parking.Persistence.Configurations;

public class PricingPolicyConfiguration : IEntityTypeConfiguration<PricingPolicy>
{
    public void Configure(EntityTypeBuilder<PricingPolicy> builder)
    {
        builder.ToTable("pricing_policies");

        builder.HasKey(p => p.Id);

        // Tạo Unique Index trên 3 cột (LotId, VehicleType, Version) để chống ghi đè dữ liệu
        // Nếu có 2 người lưu, Database sẽ văng lỗi
        builder.HasIndex(p => new { p.LotId, p.VehicleType, p.Version })
               .IsUnique()
               .HasDatabaseName("ux_pricing_policies_lot_vehicle_version");

        // Giới hạn độ dài cột chuỗi thành Varchar(50)
        builder.Property(p => p.VehicleType)
               .IsRequired()
               .HasMaxLength(50);

        builder.Property(p => p.BillingMode)
               .IsRequired()
               .HasMaxLength(50);

        // Cấu hình kiểu dữ liệu tiền tệ trong SQL là decimal(18,2)
        builder.Property(p => p.Price)
               .HasColumnType("decimal(18,2)");

        builder.Property(p => p.OvernightFee)
               .HasColumnType("decimal(18,2)");
    }
}
