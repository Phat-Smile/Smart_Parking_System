using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

// Rendering geometry is versioned independently of operational slot state.
public sealed class ParkingLayoutDocument
{
    public Guid LotId { get; set; }
    public int Version { get; set; }
    public string RenderJson { get; set; } = "[]";
}

internal sealed class ParkingLayoutDocumentConfiguration : IEntityTypeConfiguration<ParkingLayoutDocument>
{
    public void Configure(EntityTypeBuilder<ParkingLayoutDocument> builder)
    {
        builder.ToTable("parking_layout_documents", ParkingDbContext.DefaultSchema,
            table => table.HasCheckConstraint("ck_layout_documents_version_positive", "version > 0"));
        builder.HasKey(x => x.LotId).HasName("pk_parking_layout_documents");
        builder.Property(x => x.LotId).HasColumnName("lot_id").ValueGeneratedNever();
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(x => x.RenderJson).HasColumnName("render_json").HasColumnType("jsonb").IsRequired();
        builder.HasOne<ParkingLot>().WithMany().HasForeignKey(x => x.LotId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_layout_documents_parking_lots_lot_id");
    }
}
