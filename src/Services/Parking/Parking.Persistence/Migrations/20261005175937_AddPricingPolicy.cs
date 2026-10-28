using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pricing_policies",
                schema: "parking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    BillingMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BlockMinutes = table.Column<int>(type: "integer", nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OvernightFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_policies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_pricing_policies_lot_vehicle_version",
                schema: "parking",
                table: "pricing_policies",
                columns: new[] { "LotId", "VehicleType", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pricing_policies",
                schema: "parking");
        }
    }
}
