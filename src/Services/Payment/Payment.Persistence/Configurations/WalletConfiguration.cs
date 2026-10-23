using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Persistence.Configurations;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        // Tên bảng và ràng buộc cấp Database
        builder.ToTable("wallets", table =>
        {
            // Ràng buộc cấp Database: Số dư ví không bao giờ được âm
            table.HasCheckConstraint("ck_wallets_balance_non_negative", "\"Balance\" >= 0");
        });

        builder.HasKey(x => x.Id);

        // Mỗi người dùng chỉ được mở 1 ví duy nhất
        builder.HasIndex(x => x.UserId).IsUnique();

        // Tiền nong dùng kiểu numeric(12,0)
        builder.Property(x => x.Balance)
            .HasColumnType("numeric(12,0)")
            .IsRequired()
            .HasDefaultValue(0);

        // Lưu trạng thái ví dạng chữ (Active/Closed) xuống DB
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Khóa lạc quan: Chống ghi đè khi 2 giao dịch cùng sửa ví một lúc
        builder.Property(x => x.Version)
            .IsConcurrencyToken()
            .IsRequired();

        // Một ví có nhiều dòng sổ cái, cấm xóa ví nếu đã có giao dịch
        builder.HasMany(w => w.LedgerEntries)
            .WithOne()
            .HasForeignKey(e => e.WalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
