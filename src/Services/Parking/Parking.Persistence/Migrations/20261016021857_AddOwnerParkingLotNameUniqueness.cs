using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerParkingLotNameUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_parking_lots_owner_user_id_name",
                schema: "parking",
                table: "parking_lots",
                columns: new[] { "owner_user_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_parking_lots_owner_user_id_name",
                schema: "parking",
                table: "parking_lots");
        }
    }
}
