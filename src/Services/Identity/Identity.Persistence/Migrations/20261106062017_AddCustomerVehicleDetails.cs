using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerVehicleDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "brand",
                schema: "identity",
                table: "vehicles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "color",
                schema: "identity",
                table: "vehicles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fuel_type",
                schema: "identity",
                table: "vehicles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "manufacturing_year",
                schema: "identity",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "model",
                schema: "identity",
                table: "vehicles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motorbike_kind",
                schema: "identity",
                table: "vehicles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_url",
                schema: "identity",
                table: "vehicles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "seats",
                schema: "identity",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "size",
                schema: "identity",
                table: "vehicles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "brand",
                schema: "identity",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "color",
                schema: "identity",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "fuel_type",
                schema: "identity",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "manufacturing_year",
                schema: "identity",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "model",
                schema: "identity",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "motorbike_kind",
                schema: "identity",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "photo_url",
                schema: "identity",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "seats",
                schema: "identity",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "size",
                schema: "identity",
                table: "vehicles");
        }
    }
}
