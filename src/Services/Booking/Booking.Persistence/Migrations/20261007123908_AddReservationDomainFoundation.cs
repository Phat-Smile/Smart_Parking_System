using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Booking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationDomainFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reservation_capacity",
                schema: "booking",
                columns: table => new
                {
                    lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_group = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reservation_capacity", x => new { x.lot_id, x.vehicle_group });
                });

            migrationBuilder.CreateTable(
                name: "reservations",
                schema: "booking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plate_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_group = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reservation_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    appointment_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    temp_hold_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    prepaid_amount = table.Column<decimal>(type: "numeric(12,0)", precision: 12, scale: 0, nullable: false),
                    price_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    prepaid_outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    is_no_show = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reservations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reservations_lot_id_vehicle_group_status",
                schema: "booking",
                table: "reservations",
                columns: new[] { "lot_id", "vehicle_group", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_reservations_plate_number_status",
                schema: "booking",
                table: "reservations",
                columns: new[] { "plate_number", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_reservations_user_id_status",
                schema: "booking",
                table: "reservations",
                columns: new[] { "user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_capacity",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "reservations",
                schema: "booking");
        }
    }
}
