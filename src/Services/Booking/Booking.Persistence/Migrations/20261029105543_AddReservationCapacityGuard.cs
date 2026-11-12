using System;
using Microsoft.EntityFrameworkCore.Migrations;
using ParkingSystem.Booking.Domain.Reservations;

#nullable disable

namespace ParkingSystem.Booking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationCapacityGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "hold_end_at",
                schema: "booking",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "window_end_at",
                schema: "booking",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "window_start_at",
                schema: "booking",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "reservation_capacity",
                schema: "booking",
                columns: table => new
                {
                    lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_group = table.Column<VehicleGroup>(type: "booking.vehicle_group", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reservation_capacity", x => new { x.lot_id, x.vehicle_group });
                });

            migrationBuilder.CreateIndex(
                name: "ix_reservations_active_hold",
                schema: "booking",
                table: "reservations",
                columns: new[] { "lot_id", "vehicle_group", "status", "window_start_at", "hold_end_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reservations_capacity_check",
                schema: "booking",
                table: "reservations",
                columns: new[] { "lot_id", "vehicle_group", "status", "window_start_at", "window_end_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_capacity",
                schema: "booking");

            migrationBuilder.DropIndex(
                name: "ix_reservations_active_hold",
                schema: "booking",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "ix_reservations_capacity_check",
                schema: "booking",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "hold_end_at",
                schema: "booking",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "window_end_at",
                schema: "booking",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "window_start_at",
                schema: "booking",
                table: "reservations");
        }
    }
}
