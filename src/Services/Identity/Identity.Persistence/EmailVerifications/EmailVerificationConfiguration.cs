using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Identity.Domain.EmailVerifications;

namespace ParkingSystem.Identity.Persistence.EmailVerifications;

internal sealed class EmailVerificationConfiguration : IEntityTypeConfiguration<EmailVerification>
{
    public const string TableName = "email_verifications";
    public const string AccountIndexName = "ix_email_verifications_account_id_created_at_utc";
    public const string EmailIndexName = "ix_email_verifications_email_created_at_utc";

    public void Configure(EntityTypeBuilder<EmailVerification> builder)
    {
        builder.ToTable(TableName, IdentityDbContext.DefaultSchema);

        builder.HasKey(verification => verification.Id).HasName("pk_email_verifications");

        builder.Property(verification => verification.Id)
            .HasColumnName("id");

        builder.Property(verification => verification.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(verification => verification.Email)
            .HasColumnName("email")
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(verification => verification.OtpCode)
            .HasColumnName("otp_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(verification => verification.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .IsRequired();

        builder.Property(verification => verification.AttemptCount)
            .HasColumnName("attempt_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(verification => verification.IsUsed)
            .HasColumnName("is_used")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(verification => verification.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(verification => new { verification.AccountId, verification.CreatedAtUtc })
            .HasDatabaseName(AccountIndexName);

        builder.HasIndex(verification => new { verification.Email, verification.CreatedAtUtc })
            .HasDatabaseName(EmailIndexName);
    }
}
