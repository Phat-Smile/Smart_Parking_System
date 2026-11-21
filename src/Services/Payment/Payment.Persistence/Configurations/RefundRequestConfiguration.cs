using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Persistence.Configurations;

public sealed class RefundRequestConfiguration : IEntityTypeConfiguration<RefundRequest>
{
    public void Configure(EntityTypeBuilder<RefundRequest> builder)
    {
        builder.ToTable("refund_requests", PaymentDbContext.DefaultSchema);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.WalletId)
            .HasColumnName("wallet_id")
            .IsRequired();

        builder.HasIndex(x => x.WalletId);

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.LotId)
            .HasColumnName("lot_id")
            .IsRequired();

        builder.HasIndex(x => x.LotId);

        builder.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(12,0)")
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.ReferenceType)
            .HasColumnName("reference_type")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.ReferenceId)
            .HasColumnName("reference_id")
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(x => x.OwnerConfirmedBy)
            .HasColumnName("owner_confirmed_by");

        builder.Property(x => x.ReviewedBy)
            .HasColumnName("reviewed_by");

        builder.Property(x => x.ReviewedAt)
            .HasColumnName("reviewed_at");

        builder.Property(x => x.LedgerEntryId)
            .HasColumnName("ledger_entry_id");

        builder.HasIndex(x => x.LedgerEntryId)
            .IsUnique();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(x => new { x.Status, x.CreatedAt });

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
