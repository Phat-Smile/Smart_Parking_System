using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

internal sealed class ParkingLayoutChangeRequestConfiguration : IEntityTypeConfiguration<ParkingLayoutChangeRequest>
{
    public void Configure(EntityTypeBuilder<ParkingLayoutChangeRequest> builder)
    {
        builder.ToTable("parking_layout_change_requests", ParkingDbContext.DefaultSchema);
        builder.HasKey(x => x.Id).HasName("pk_parking_layout_change_requests");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.LotId).HasColumnName("lot_id").IsRequired();
        builder.Property(x => x.RequestedBy).HasColumnName("requested_by").IsRequired();
        builder.Property(x => x.ExpectedVersion).HasColumnName("expected_version").IsRequired();
        builder.Property(x => x.ProposedLayoutJson).HasColumnName("proposed_layout_json")
            .HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status")
            .HasConversion<string>().HasMaxLength(20).IsRequired().IsConcurrencyToken();
        builder.Property(x => x.RequestedAtUtc).HasColumnName("requested_at_utc").IsRequired();
        builder.Property(x => x.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(x => x.ReviewedAtUtc).HasColumnName("reviewed_at_utc");
        builder.Property(x => x.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(500);
        builder.HasOne<ParkingLot>().WithMany().HasForeignKey(x => x.LotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_layout_change_requests_parking_lots_lot_id");
        // Database constraint prevents two simultaneous pending proposals for one lot.
        builder.HasIndex(x => x.LotId)
            .HasDatabaseName("ux_parking_layout_change_requests_pending_lot")
            .IsUnique()
            .HasFilter("status = 'Pending'");
    }
}
