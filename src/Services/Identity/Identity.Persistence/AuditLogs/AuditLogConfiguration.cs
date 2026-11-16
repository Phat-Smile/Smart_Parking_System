using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Identity.Domain.AuditLogs;

namespace ParkingSystem.Identity.Persistence.AuditLogs;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs", IdentityDbContext.DefaultSchema);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        // Sequence tự tăng làm tie-breaker khi sắp xếp
        builder.Property(x => x.Seq)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.OccurredAtUtc)
            .IsRequired();

        builder.Property(x => x.RecordedAtUtc)
            .IsRequired();

        builder.Property(x => x.SourceService)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.SourceEntryId)
            .IsRequired();

        builder.Property(x => x.ActionType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ActorId);

        builder.Property(x => x.ActorEmail)
            .HasMaxLength(254);

        builder.Property(x => x.ActorRole)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ParkingLotId);

        builder.Property(x => x.TargetType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.TargetId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.IpAddress)
            .HasMaxLength(45);

        // Lưu JSON chi tiết trong PostgreSQL
        builder.Property(x => x.Details)
            .HasColumnType("jsonb");

        // Unique index chống trùng lặp sự kiện
        builder.HasIndex(x => new { x.SourceService, x.SourceEntryId })
            .IsUnique()
            .HasDatabaseName("ux_identity_audit_source_entry");

        // Index tối ưu truy vấn phân trang theo thời gian
        builder.HasIndex(x => new { x.OccurredAtUtc, x.Seq })
            .HasDatabaseName("ix_identity_audit_occurred");

        // Index lọc theo loại hành động
        builder.HasIndex(x => new { x.ActionType, x.OccurredAtUtc })
            .HasDatabaseName("ix_identity_audit_action");

        // Index lọc theo người thực hiện
        builder.HasIndex(x => new { x.ActorId, x.OccurredAtUtc })
            .HasDatabaseName("ix_identity_audit_actor");
    }
}
