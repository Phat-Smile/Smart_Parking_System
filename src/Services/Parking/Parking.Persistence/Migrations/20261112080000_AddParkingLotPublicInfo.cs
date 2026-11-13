using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations;

[DbContext(typeof(ParkingDbContext))]
[Migration("20261112080000_AddParkingLotPublicInfo")]
public sealed class AddParkingLotPublicInfo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "public_info", schema: "parking",
            table: "parking_lots", type: "jsonb", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "public_info", schema: "parking", table: "parking_lots");
}
