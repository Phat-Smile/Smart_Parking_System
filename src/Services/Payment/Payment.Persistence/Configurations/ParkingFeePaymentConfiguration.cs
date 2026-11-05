using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Payment.Domain.Constants;
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

        builder.Property(x => x.PaymentMethod)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.TxnRef)
            .HasMaxLength(32);

        builder.Property(x => x.PaymentUrl)
            .HasMaxLength(2048);

        builder.Property(x => x.GatewayTransactionNo)
            .HasMaxLength(50);

        builder.Property(x => x.FailureCode)
            .HasMaxLength(50);

        builder.Property(x => x.SurplusRefundStatus)
            .HasMaxLength(30);

        // Khóa ngoại có thể null cho khách vãng lai
        builder.Property(x => x.WalletId).IsRequired(false);
        builder.Property(x => x.AccountId).IsRequired(false);

        // Đánh chỉ mục tìm nhanh
        builder.HasIndex(x => x.SessionId);
        builder.HasIndex(x => x.AccountId);
        builder.HasIndex(x => x.WalletId);

        // Khóa phiên bản chống ghi đè đồng thời
        builder.Property<uint>("Version")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Mã giao dịch là duy nhất trên toàn hệ thống
        builder.HasIndex(x => x.TxnRef)
            .IsUnique()
            .HasDatabaseName("ux_fee_payments_vnpay_txn_ref");

        // Chỉ mục duy nhất: Mỗi phiên đỗ chỉ có 1 mã Pending
        builder.HasIndex(x => x.SessionId)
            .HasDatabaseName("ux_fee_payments_session_pending")
            .HasFilter($"\"Status\" = '{PaymentStatus.Pending}'")
            .IsUnique();

        // Chỉ mục duy nhất: Mỗi phiên đỗ chỉ có 1 giao dịch thành công
        builder.HasIndex(x => x.SessionId)
            .HasDatabaseName("ux_fee_payments_session_succeeded")
            .HasFilter($"\"Status\" = '{PaymentStatus.Succeeded}'")
            .IsUnique();
    }
}
