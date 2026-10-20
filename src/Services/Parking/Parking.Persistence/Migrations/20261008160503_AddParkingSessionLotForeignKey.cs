using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParkingSessionLotForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "fk_parking_sessions_parking_lots_lot_id",
                schema: "parking",
                table: "parking_sessions",
                column: "lot_id",
                principalSchema: "parking",
                principalTable: "parking_lots",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_parking_sessions_parking_lots_lot_id",
                schema: "parking",
                table: "parking_sessions");
        }
    }
}
