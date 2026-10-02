using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ParkingSystem.Payment.Persistence.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", PaymentDbContext.DefaultSchema);
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(message => message.EventType).HasColumnName("event_type").HasMaxLength(512).IsRequired();
        builder.Property(message => message.EventVersion).HasColumnName("event_version").HasDefaultValue(1).IsRequired();
        builder.Property(message => message.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(message => message.ProcessedAtUtc).HasColumnName("processed_at_utc");
        builder.Property(message => message.RetryCount).HasColumnName("retry_count").HasDefaultValue(0).IsRequired();
        builder.Property(message => message.Error).HasColumnName("error");
        builder.Property(message => message.CorrelationId).HasColumnName("correlation_id");
        builder.Property(message => message.CausationId).HasColumnName("causation_id");
        builder.Property(message => message.LockId).HasColumnName("lock_id");
        builder.Property(message => message.LockedUntilUtc).HasColumnName("locked_until_utc");
        builder.Property(message => message.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");
        builder.HasIndex(message => message.OccurredAtUtc)
            .HasDatabaseName("ix_outbox_messages_pending")
            .HasFilter("processed_at_utc IS NULL");
    }
}
