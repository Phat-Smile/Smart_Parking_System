using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParkingSessionZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // pricing_policies is created by 20261005175937_AddPricingPolicy; it was only missing from the snapshot on develop,
            // so EF generated it again here. The snapshot now includes it and this migration leaves it alone.
            migrationBuilder.AddColumn<Guid>(
                name: "zone_id",
                schema: "parking",
                table: "parking_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_parking_sessions_zone_id",
                schema: "parking",
                table: "parking_sessions",
                column: "zone_id");

            migrationBuilder.AddForeignKey(
                name: "fk_parking_sessions_zones_zone_id",
                schema: "parking",
                table: "parking_sessions",
                column: "zone_id",
                principalSchema: "parking",
                principalTable: "zones",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_parking_sessions_zones_zone_id",
                schema: "parking",
                table: "parking_sessions");

            migrationBuilder.DropIndex(
                name: "ix_parking_sessions_zone_id",
                schema: "parking",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "zone_id",
                schema: "parking",
                table: "parking_sessions");
        }
    }
}
