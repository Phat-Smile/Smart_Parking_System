using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Persistence.Accounts;

internal sealed class OwnerApplicationDocumentConfiguration : IEntityTypeConfiguration<OwnerApplicationDocument>
{
    internal const string AccountDocumentTypeIndexName = "ix_owner_application_documents_account_id_document_type";
    internal const string StorageKeyIndexName = "ux_owner_application_documents_storage_key";

    public void Configure(EntityTypeBuilder<OwnerApplicationDocument> builder)
    {
        builder.ToTable("owner_application_documents", IdentityDbContext.DefaultSchema);
        builder.HasKey(doc => doc.Id).HasName("pk_owner_application_documents");
        builder.Property(doc => doc.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(doc => doc.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(doc => doc.DocumentType).HasColumnName("document_type").HasMaxLength(OwnerApplicationDocument.MaxDocumentTypeLength).IsRequired();
        builder.Property(doc => doc.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(OwnerApplicationDocument.MaxFileNameLength).IsRequired();
        builder.Property(doc => doc.ContentType).HasColumnName("content_type").HasMaxLength(OwnerApplicationDocument.MaxContentTypeLength).IsRequired();
        builder.Property(doc => doc.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(doc => doc.Sha256).HasColumnName("sha256").HasMaxLength(OwnerApplicationDocument.Sha256Length).IsRequired();
        builder.Property(doc => doc.StorageKey).HasColumnName("storage_key").HasMaxLength(OwnerApplicationDocument.MaxStorageKeyLength).IsRequired();
        builder.Property(doc => doc.UploadedAtUtc).HasColumnName("uploaded_at_utc").IsRequired();
        builder.Property(doc => doc.PurgedAtUtc).HasColumnName("purged_at_utc");

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(doc => doc.AccountId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_owner_application_documents_accounts_account_id");

        builder.HasIndex(doc => new { doc.AccountId, doc.DocumentType })
            .IsUnique()
            .HasDatabaseName(AccountDocumentTypeIndexName);

        builder.HasIndex(doc => doc.StorageKey)
            .IsUnique()
            .HasDatabaseName(StorageKeyIndexName);
    }
}
