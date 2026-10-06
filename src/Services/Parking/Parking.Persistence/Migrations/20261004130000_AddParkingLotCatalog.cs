using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParkingLotCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parking_lots",
                schema: "parking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    address = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    approval_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parking_lots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_parking_lots_approval_status_name",
                schema: "parking",
                table: "parking_lots",
                columns: new[] { "approval_status", "name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parking_lots",
                schema: "parking");
        }
    }
}
