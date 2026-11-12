using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Identity.Domain.Accounts;
using ParkingSystem.Identity.Domain.Profiles;

namespace ParkingSystem.Identity.Persistence.Profiles;

internal sealed class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.ToTable("profiles", IdentityDbContext.DefaultSchema);

        builder.HasKey(profile => profile.AccountId).HasName("pk_profiles");
        builder.Property(profile => profile.AccountId).HasColumnName("account_id").ValueGeneratedNever();

        builder.Property(profile => profile.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(Profile.MaxFullNameLength)
            .IsRequired();

        builder.Property(profile => profile.DateOfBirth)
            .HasColumnName("date_of_birth")
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(profile => profile.CitizenIdNumber)
            .HasColumnName("citizen_id_number")
            .HasMaxLength(20);

        builder.Property(profile => profile.DriverLicenseNumber)
            .HasColumnName("driver_license_number")
            .HasMaxLength(20);

        builder.Property(profile => profile.AvatarUrl)
            .HasColumnName("avatar_url")
            .HasMaxLength(500);

        builder.Property(profile => profile.Address)
            .HasColumnName("address")
            .HasMaxLength(500);
        builder.Property(x => x.IdentityDocumentType).HasColumnName("identity_document_type").HasMaxLength(30);
        builder.Property(x => x.IdentityIssueDate).HasColumnName("identity_issue_date");
        builder.Property(x => x.IdentityIssuedBy).HasColumnName("identity_issued_by").HasMaxLength(150);
        builder.Property(x => x.IdentityFrontMediaId).HasColumnName("identity_front_media_id");
        builder.Property(x => x.IdentityBackMediaId).HasColumnName("identity_back_media_id");

        builder.Property(profile => profile.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(profile => profile.UpdatedAtUtc)
            .HasColumnName("updated_at_utc");

        builder.HasOne<Account>()
            .WithOne()
            .HasForeignKey<Profile>(profile => profile.AccountId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_profiles_accounts_account_id");
    }
}
