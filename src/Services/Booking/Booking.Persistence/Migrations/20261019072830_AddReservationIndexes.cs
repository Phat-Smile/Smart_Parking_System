using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Booking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_reservations_lot_id_status",
                schema: "booking",
                table: "reservations",
                columns: new[] { "lot_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_reservations_status_temp_hold_expires_at",
                schema: "booking",
                table: "reservations",
                columns: new[] { "status", "temp_hold_expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reservations_user_id",
                schema: "booking",
                table: "reservations",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reservations_lot_id_status",
                schema: "booking",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "ix_reservations_status_temp_hold_expires_at",
                schema: "booking",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "ix_reservations_user_id",
                schema: "booking",
                table: "reservations");
        }
    }
}
