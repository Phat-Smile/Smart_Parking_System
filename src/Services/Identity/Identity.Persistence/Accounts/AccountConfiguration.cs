using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Persistence.Accounts;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    internal const string EmailIndexName = "ux_accounts_email";
    internal const string PhoneNumberIndexName = "ux_accounts_phone_number";
    internal const string GoogleSubjectIndexName = "ux_accounts_google_subject";

    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts", IdentityDbContext.DefaultSchema);
        builder.HasKey(account => account.Id).HasName("pk_accounts");
        builder.Property(account => account.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(account => account.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(account => account.PasswordHash).HasColumnName("password_hash").HasMaxLength(100);
        builder.Property(account => account.GoogleSubject).HasColumnName("google_subject").HasMaxLength(255);
        builder.HasIndex(account => account.GoogleSubject)
            .IsUnique()
            .HasFilter("google_subject IS NOT NULL")
            .HasDatabaseName(GoogleSubjectIndexName);
        builder.Ignore(account => account.HasPassword);
        builder.Property(account => account.PhoneNumber).HasColumnName("phone_number").HasMaxLength(16);
        builder.Property(account => account.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(account => account.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(32).HasDefaultValue(AccountRole.Customer).IsRequired();
        builder.Property(account => account.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(account => account.FailedLoginAttempts).HasColumnName("failed_login_attempts").HasDefaultValue(0).IsRequired();
        builder.Property(account => account.LockoutEndUtc).HasColumnName("lockout_end_utc");
        builder.Property(account => account.RowVersion).HasColumnName("row_version").HasDefaultValue(1).IsConcurrencyToken().IsRequired();
        builder.HasIndex(account => account.Email).IsUnique().HasDatabaseName(EmailIndexName);
        builder.HasIndex(account => account.PhoneNumber)
            .IsUnique()
            .HasFilter("phone_number IS NOT NULL")
            .HasDatabaseName(PhoneNumberIndexName);
    }
}
