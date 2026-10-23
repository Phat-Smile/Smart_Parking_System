using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations;

public partial class AddParkingLotOpeningHours : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(
            name: "opening_hours",
            schema: "parking",
            table: "parking_lots",
            type: "jsonb",
            nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "opening_hours", schema: "parking", table: "parking_lots");
}
