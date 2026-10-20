using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Persistence.Configurations;

public class WalletLedgerEntryConfiguration : IEntityTypeConfiguration<WalletLedgerEntry>
{
    public void Configure(EntityTypeBuilder<WalletLedgerEntry> builder)
    {
        // Tên bảng lịch sử giao dịch và ràng buộc Database
        builder.ToTable("wallet_ledger_entries", table =>
        {
            // Ràng buộc Database: Số dư sau giao dịch không bao giờ được âm
            table.HasCheckConstraint("ck_wallet_ledger_entries_balance_after_non_negative", "\"BalanceAfter\" >= 0");
        });

        builder.HasKey(x => x.Id);

        // ID được sinh trước bằng Guid trong code C#, không để Database tự sinh
        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        // Tiền dùng kiểu numeric(12,0)
        builder.Property(x => x.Amount)
            .HasColumnType("numeric(12,0)")
            .IsRequired();

        builder.Property(x => x.BalanceAfter)
            .HasColumnType("numeric(12,0)")
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(500);

        builder.Property(x => x.ReferenceId)
            .HasMaxLength(100);

        // Lưu loại giao dịch dạng chuỗi text xuống DB
        builder.Property(x => x.EntryType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.ReferenceType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(100);

        // Chống ghi đúp giao dịch trên Database:
        // Cùng một ví và loại giao dịch thì không được trùng mã IdempotencyKey
        // Chỉ áp dụng với các giao dịch có mã (bỏ qua các bản ghi có key null)
        builder.HasIndex(x => new { x.WalletId, x.EntryType, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");

        // Trỏ về ID của một giao dịch bị sai trước đó
        builder.HasOne<WalletLedgerEntry>()
            .WithMany()
            .HasForeignKey(x => x.ReversalOfEntryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
