using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ParkingSystem.Parking.Persistence.Auditing;

// Columns follow the team ERD (parking.audit_logs).
internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_logs", ParkingDbContext.DefaultSchema);
        builder.HasKey(entry => entry.Id).HasName("pk_audit_logs");

        builder.Property(entry => entry.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entry => entry.LotId).HasColumnName("lot_id");
        builder.Property(entry => entry.ActorUserId).HasColumnName("actor_user_id").IsRequired();
        builder.Property(entry => entry.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.TargetType).HasColumnName("target_type").HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.TargetId).HasColumnName("target_id").HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(entry => entry.Details).HasColumnName("details").HasColumnType("jsonb");
        builder.Property(entry => entry.OccurredAt).HasColumnName("occurred_at").IsRequired();

        // ERD has a foreign key lot_id -> parking_lots. It is added together with the lot-existence check on
        // parking_sessions.lot_id, because sessions can still reference a lot that is not in parking_lots yet.

        // Admins and owners read the log per lot, newest first (NFR-16).
        builder.HasIndex(entry => new { entry.LotId, entry.OccurredAt })
            .HasDatabaseName("ix_audit_logs_lot_occurred_at");
        builder.HasIndex(entry => new { entry.Action, entry.OccurredAt })
            .HasDatabaseName("ix_audit_logs_action_occurred_at");
        builder.HasIndex(entry => new { entry.TargetType, entry.TargetId })
            .HasDatabaseName("ix_audit_logs_target");
    }
}
