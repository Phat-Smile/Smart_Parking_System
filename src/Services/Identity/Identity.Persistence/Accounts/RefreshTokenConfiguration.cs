using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Persistence.Accounts;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    internal const string AccountIndexName = "ix_refresh_tokens_account_id";
    internal const string TokenHashIndexName = "ux_refresh_tokens_token_hash";

    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", IdentityDbContext.DefaultSchema);

        builder.HasKey(rt => rt.Id).HasName("pk_refresh_tokens");
        builder.Property(rt => rt.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(rt => rt.AccountId).HasColumnName("account_id").IsRequired();

        builder.Property(rt => rt.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();

        builder.Property(rt => rt.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();

        builder.Property(rt => rt.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.Property(rt => rt.RevokedAtUtc).HasColumnName("revoked_at_utc");

        builder.Property(rt => rt.ReplacedByTokenHash).HasColumnName("replaced_by_token_hash").HasMaxLength(64);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(rt => rt.AccountId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_refresh_tokens_accounts_account_id");

        builder.HasIndex(rt => rt.AccountId).HasDatabaseName(AccountIndexName);

        builder.HasIndex(rt => rt.TokenHash).IsUnique().HasDatabaseName(TokenHashIndexName);
    }
}
