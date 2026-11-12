using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ParkingSystem.Notification.Persistence.CustomerNotifications;

internal sealed class CustomerNotificationConfiguration : IEntityTypeConfiguration<CustomerNotification>
{
    public void Configure(EntityTypeBuilder<CustomerNotification> builder)
    {
        builder.ToTable("customer_notifications", NotificationDbContext.DefaultSchema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.SourceEventKey).HasColumnName("source_event_key").HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.SourceEventKey).IsUnique();
        builder.Property(x => x.Type).HasColumnName("type").HasMaxLength(30).IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(160).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Href).HasColumnName("href").HasMaxLength(200).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.ReadAtUtc).HasColumnName("read_at_utc");
        builder.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
    }
}
