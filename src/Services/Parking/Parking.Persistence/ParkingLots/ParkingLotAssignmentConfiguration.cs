using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

internal sealed class ParkingLotAssignmentConfiguration : IEntityTypeConfiguration<ParkingLotAssignment>
{
    public void Configure(EntityTypeBuilder<ParkingLotAssignment> builder)
    {
        builder.ToTable("parking_lot_assignments", ParkingDbContext.DefaultSchema);
        // Khoá (lot_id, account_id): mỗi tài khoản chỉ có một vai trò trong một bãi.
        builder.HasKey(assignment => new { assignment.LotId, assignment.AccountId }).HasName("pk_parking_lot_assignments");

        builder.Property(assignment => assignment.LotId).HasColumnName("lot_id");
        builder.Property(assignment => assignment.AccountId).HasColumnName("account_id");
        builder.Property(assignment => assignment.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(assignment => assignment.AssignedBy).HasColumnName("assigned_by").IsRequired();
        builder.Property(assignment => assignment.AssignedAtUtc).HasColumnName("assigned_at_utc").IsRequired();

        builder.HasOne<ParkingLot>()
            .WithMany()
            .HasForeignKey(assignment => assignment.LotId)
            .HasConstraintName("fk_parking_lot_assignments_parking_lots_lot_id")
            .OnDelete(DeleteBehavior.Cascade);

        // Tra "người này làm ở những bãi nào" theo account_id.
        builder.HasIndex(assignment => assignment.AccountId).HasDatabaseName("ix_parking_lot_assignments_account_id");
    }
}
