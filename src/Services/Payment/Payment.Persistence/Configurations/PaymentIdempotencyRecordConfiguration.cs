using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Persistence.Configurations;

// Cấu hình bảng khóa chống trừ tiền trùng
public sealed class PaymentIdempotencyRecordConfiguration : IEntityTypeConfiguration<PaymentIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<PaymentIdempotencyRecord> builder)
    {
        builder.ToTable("payment_idempotency_records");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Operation)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.RequestHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(20)
            .IsRequired();

        // Chống gọi lặp cho cùng một giao dịch
        builder.HasIndex(x => new { x.AccountId, x.Operation, x.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ux_payment_idempotency_account_op_key");
    }
}
