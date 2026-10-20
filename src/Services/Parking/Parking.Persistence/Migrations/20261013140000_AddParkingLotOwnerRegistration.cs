using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations;

// Additive migration: existing seed/catalog rows remain valid (owner_user_id is null).
[DbContext(typeof(ParkingDbContext))]
[Migration("20261013140000_AddParkingLotOwnerRegistration")]
public sealed partial class AddParkingLotOwnerRegistration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "owner_user_id",
            schema: "parking",
            table: "parking_lots",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_parking_lots_owner_user_id",
            schema: "parking",
            table: "parking_lots",
            column: "owner_user_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_parking_lots_owner_user_id",
            schema: "parking",
            table: "parking_lots");

        migrationBuilder.DropColumn(
            name: "owner_user_id",
            schema: "parking",
            table: "parking_lots");
    }
}
