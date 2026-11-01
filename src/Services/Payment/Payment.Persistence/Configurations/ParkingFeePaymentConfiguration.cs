using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Persistence.Configurations;

// Cấu hình bảng hóa đơn tiền đỗ xe
public sealed class ParkingFeePaymentConfiguration : IEntityTypeConfiguration<ParkingFeePayment>
{
    public void Configure(EntityTypeBuilder<ParkingFeePayment> builder)
    {
        builder.ToTable("parking_fee_payments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.PlateNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.VehicleType)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(20)
            .IsRequired();

        // Đánh chỉ mục tìm nhanh theo phiên đỗ xe
        builder.HasIndex(x => x.SessionId);
        builder.HasIndex(x => x.AccountId);
        builder.HasIndex(x => x.WalletId);
    }
}
