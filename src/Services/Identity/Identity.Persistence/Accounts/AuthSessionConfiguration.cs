using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Persistence.Accounts;

internal sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    internal const string AccountIndexName = "ix_auth_sessions_account_id";
    internal const string AbsoluteExpiresAtIndexName = "ix_auth_sessions_absolute_expires_at_utc";

    public void Configure(EntityTypeBuilder<AuthSession> builder)
    {
        builder.ToTable("auth_sessions", IdentityDbContext.DefaultSchema);

        builder.HasKey(s => s.Id).HasName("pk_auth_sessions");
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(s => s.AccountId).HasColumnName("account_id").IsRequired();

        builder.Property(s => s.DeviceInfo).HasColumnName("device_info").HasMaxLength(512);

        builder.Property(s => s.IpAddress).HasColumnName("ip_address").HasMaxLength(64);

        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.Property(s => s.LastActivityAtUtc).HasColumnName("last_activity_at_utc").IsRequired();

        builder.Property(s => s.AbsoluteExpiresAtUtc).HasColumnName("absolute_expires_at_utc").IsRequired();

        builder.Property(s => s.RevokedAtUtc).HasColumnName("revoked_at_utc");

        builder.Property(s => s.RevokedReason).HasColumnName("revoked_reason").HasMaxLength(128);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(s => s.AccountId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_auth_sessions_accounts_account_id");

        builder.HasIndex(s => s.AccountId).HasDatabaseName(AccountIndexName);

        builder.HasIndex(s => s.AbsoluteExpiresAtUtc).HasDatabaseName(AbsoluteExpiresAtIndexName);
    }
}
