using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Persistence.Configurations;

public class VnpayTransactionConfiguration : IEntityTypeConfiguration<VnpayTransaction>
{
    public void Configure(EntityTypeBuilder<VnpayTransaction> builder)
    {
        builder.ToTable("vnpay_transactions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        // Mã tham chiếu giao dịch VNPay (32 hex characters)
        builder.Property(x => x.TxnRef)
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(x => x.TxnRef)
            .IsUnique();

        // Ràng buộc khóa ngoại về Ví
        builder.Property(x => x.WalletId)
            .IsRequired();

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Restrict);

        // Số tiền giao dịch VNPay
        builder.Property(x => x.Amount)
            .HasColumnType("numeric(12,0)")
            .IsRequired();

        // Mục đích và Trạng thái lưu dạng chuỗi
        builder.Property(x => x.Purpose)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Khóa Idempotency từ Header client (8-64 ký tự)
        builder.Property(x => x.ClientRequestKey)
            .HasMaxLength(64)
            .IsRequired();

        // Ràng buộc duy nhất cấp Database: một ví không thể có 2 giao dịch cùng Idempotency-Key
        builder.HasIndex(x => new { x.WalletId, x.ClientRequestKey })
            .IsUnique();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .IsRequired();

        builder.Property(x => x.CompletedAt);

        // Các trường đối soát từ VNPay (nullable khi mới khởi tạo)
        builder.Property(x => x.VnpayTransactionNo)
            .HasMaxLength(64);

        builder.Property(x => x.VnpayBankCode)
            .HasMaxLength(20);

        builder.Property(x => x.VnpayResponseCode)
            .HasMaxLength(10);

        // Khóa lạc quan chống cộng tiền trùng lặp khi VNPay retry nhiều IPN cùng lúc
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
