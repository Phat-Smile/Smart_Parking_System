using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentConfirmedPlate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "confirmed_plate",
                schema: "parking",
                table: "incidents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "confirmed_vehicle_type",
                schema: "parking",
                table: "incidents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "confirmed_plate",
                schema: "parking",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "confirmed_vehicle_type",
                schema: "parking",
                table: "incidents");
        }
    }
}
