using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Booking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:booking.cancel_reason", "customer_cancelled,payment_failed,no_show,no_suitable_slot")
                .Annotation("Npgsql:Enum:booking.prepaid_outcome", "applied,refunded,forfeited")
                .Annotation("Npgsql:Enum:booking.reservation_status", "temporary_hold,confirmed,used,cancelled,expired")
                .Annotation("Npgsql:Enum:booking.reservation_type", "hold_while_driving,scheduled")
                .Annotation("Npgsql:Enum:booking.vehicle_group", "car,motorbike");

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
                    vehicle_group = table.Column<int>(type: "booking.vehicle_group", nullable: false),
                    reservation_type = table.Column<int>(type: "booking.reservation_type", nullable: false),
                    status = table.Column<int>(type: "booking.reservation_status", nullable: false),
                    appointment_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    temp_hold_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    prepaid_amount = table.Column<decimal>(type: "numeric(12,0)", precision: 12, scale: 0, nullable: false),
                    price_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_reason = table.Column<int>(type: "booking.cancel_reason", nullable: true),
                    prepaid_outcome = table.Column<int>(type: "booking.prepaid_outcome", nullable: true),
                    is_no_show = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reservations", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservations",
                schema: "booking");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:Enum:booking.cancel_reason", "customer_cancelled,payment_failed,no_show,no_suitable_slot")
                .OldAnnotation("Npgsql:Enum:booking.prepaid_outcome", "applied,refunded,forfeited")
                .OldAnnotation("Npgsql:Enum:booking.reservation_status", "temporary_hold,confirmed,used,cancelled,expired")
                .OldAnnotation("Npgsql:Enum:booking.reservation_type", "hold_while_driving,scheduled")
                .OldAnnotation("Npgsql:Enum:booking.vehicle_group", "car,motorbike");
        }
    }
}
