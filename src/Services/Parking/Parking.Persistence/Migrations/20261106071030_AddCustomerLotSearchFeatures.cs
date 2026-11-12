using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerLotSearchFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_cctv",
                schema: "parking",
                table: "parking_lots",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "has_ev_charging",
                schema: "parking",
                table: "parking_lots",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_covered",
                schema: "parking",
                table: "parking_lots",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "supports_electric_vehicles",
                schema: "parking",
                table: "parking_lots",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "supports_large_vehicles",
                schema: "parking",
                table: "parking_lots",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_cctv",
                schema: "parking",
                table: "parking_lots");

            migrationBuilder.DropColumn(
                name: "has_ev_charging",
                schema: "parking",
                table: "parking_lots");

            migrationBuilder.DropColumn(
                name: "is_covered",
                schema: "parking",
                table: "parking_lots");

            migrationBuilder.DropColumn(
                name: "supports_electric_vehicles",
                schema: "parking",
                table: "parking_lots");

            migrationBuilder.DropColumn(
                name: "supports_large_vehicles",
                schema: "parking",
                table: "parking_lots");
        }
    }
}
