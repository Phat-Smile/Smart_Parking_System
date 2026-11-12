using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Mfa;

namespace ParkingSystem.Identity.Persistence.Mfa;

internal sealed class AccountMfaConfiguration : IEntityTypeConfiguration<AccountMfa>
{
    public void Configure(EntityTypeBuilder<AccountMfa> builder)
    {
        builder.ToTable("account_mfa", IdentityDbContext.DefaultSchema);
        // Mỗi tài khoản tối đa một cấu hình TOTP.
        builder.HasKey(m => m.AccountId).HasName("pk_account_mfa");
        builder.Property(m => m.AccountId).HasColumnName("account_id").ValueGeneratedNever();
        builder.Property(m => m.SecretProtected).HasColumnName("secret_protected").IsRequired();
        builder.Property(m => m.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(m => m.EnabledAtUtc).HasColumnName("enabled_at_utc");
        builder.Property(m => m.LastUsedStep).HasColumnName("last_used_step").IsRequired();
        builder.Ignore(m => m.IsEnabled);

        builder.HasOne<Account>().WithOne().HasForeignKey<AccountMfa>(m => m.AccountId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_account_mfa_accounts_account_id");
    }
}

internal sealed class MfaRecoveryCodeConfiguration : IEntityTypeConfiguration<MfaRecoveryCode>
{
    public void Configure(EntityTypeBuilder<MfaRecoveryCode> builder)
    {
        builder.ToTable("mfa_recovery_codes", IdentityDbContext.DefaultSchema);
        builder.HasKey(c => c.Id).HasName("pk_mfa_recovery_codes");
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(c => c.CodeHash).HasColumnName("code_hash").HasMaxLength(64).IsRequired();
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(c => c.UsedAtUtc).HasColumnName("used_at_utc");

        builder.HasOne<Account>().WithMany().HasForeignKey(c => c.AccountId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_mfa_recovery_codes_accounts_account_id");
        builder.HasIndex(c => c.AccountId).HasDatabaseName("ix_mfa_recovery_codes_account_id");
    }
}

internal sealed class MfaChallengeConfiguration : IEntityTypeConfiguration<MfaChallenge>
{
    public void Configure(EntityTypeBuilder<MfaChallenge> builder)
    {
        builder.ToTable("mfa_challenges", IdentityDbContext.DefaultSchema);
        builder.HasKey(c => c.Id).HasName("pk_mfa_challenges");
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.AccountId).HasColumnName("account_id").IsRequired();
        builder.Property(c => c.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(c => c.AuthenticationMethod).HasColumnName("authentication_method").HasMaxLength(16).IsRequired();
        builder.Property(c => c.DeviceInfo).HasColumnName("device_info").HasMaxLength(AuthSession.MaxDeviceInfoLength);
        builder.Property(c => c.IpAddress).HasColumnName("ip_address").HasMaxLength(AuthSession.MaxIpAddressLength);
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(c => c.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(c => c.UsedAtUtc).HasColumnName("used_at_utc");
        builder.Property(c => c.AttemptCount).HasColumnName("attempt_count").IsRequired();

        builder.HasOne<Account>().WithMany().HasForeignKey(c => c.AccountId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_mfa_challenges_accounts_account_id");
        builder.HasIndex(c => c.TokenHash).IsUnique().HasDatabaseName("ux_mfa_challenges_token_hash");
        builder.HasIndex(c => c.AccountId).HasDatabaseName("ix_mfa_challenges_account_id");
    }
}
