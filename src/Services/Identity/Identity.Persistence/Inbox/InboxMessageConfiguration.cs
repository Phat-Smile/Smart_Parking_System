using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ParkingSystem.Identity.Persistence.Inbox;

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages", IdentityDbContext.DefaultSchema);
        builder.HasKey(message => new { message.MessageId, message.ConsumerKey }).HasName("pk_inbox_messages");
        builder.Property(message => message.MessageId).HasColumnName("message_id").ValueGeneratedNever();
        builder.Property(message => message.ConsumerKey).HasColumnName("consumer_key").HasMaxLength(512).IsRequired();
        builder.Property(message => message.ReceivedAtUtc).HasColumnName("received_at_utc").IsRequired();
        builder.Property(message => message.ProcessedAtUtc).HasColumnName("processed_at_utc").IsRequired();
    }
}