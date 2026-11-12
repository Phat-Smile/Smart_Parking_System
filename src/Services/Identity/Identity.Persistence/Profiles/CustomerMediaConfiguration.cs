using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ParkingSystem.Identity.Persistence.Profiles;

internal sealed class CustomerMediaConfiguration : IEntityTypeConfiguration<CustomerMedia>
{
    public void Configure(EntityTypeBuilder<CustomerMedia> builder)
    {
        builder.ToTable("customer_media", IdentityDbContext.DefaultSchema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(32).IsRequired();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.SizeBytes).HasColumnName("size_bytes").IsRequired();
        builder.Property(x => x.StorageKey).HasColumnName("storage_key").HasMaxLength(80).IsRequired();
        builder.Property(x => x.UploadedAtUtc).HasColumnName("uploaded_at_utc").IsRequired();
        builder.HasIndex(x => x.AccountId);
        builder.HasIndex(x => x.StorageKey).IsUnique();
    }
}
