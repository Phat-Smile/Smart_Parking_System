using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations;

public partial class AddOwnerLayoutRenderDocument : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "parking_layout_documents",
            schema: "parking",
            columns: table => new
            {
                lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                version = table.Column<int>(type: "integer", nullable: false),
                render_json = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_parking_layout_documents", x => x.lot_id);
                table.ForeignKey(
                    name: "fk_layout_documents_parking_lots_lot_id",
                    column: x => x.lot_id,
                    principalSchema: "parking",
                    principalTable: "parking_lots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("ck_layout_documents_version_positive", "version > 0");
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "parking_layout_documents", schema: "parking");
}
