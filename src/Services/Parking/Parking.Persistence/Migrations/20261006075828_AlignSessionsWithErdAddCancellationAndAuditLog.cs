using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignSessionsWithErdAddCancellationAndAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "entered_at",
                schema: "parking",
                table: "parking_sessions",
                newName: "entry_time");

            migrationBuilder.RenameColumn(
                name: "booking_id",
                schema: "parking",
                table: "parking_sessions",
                newName: "reservation_id");

            migrationBuilder.AddColumn<string>(
                name: "cancel_reason",
                schema: "parking",
                table: "parking_sessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelled_at",
                schema: "parking",
                table: "parking_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by",
                schema: "parking",
                table: "parking_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "parking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    details = table.Column<string>(type: "jsonb", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_lot_occurred_at",
                schema: "parking",
                table: "audit_logs",
                columns: new[] { "lot_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_target",
                schema: "parking",
                table: "audit_logs",
                columns: new[] { "target_type", "target_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "parking");

            migrationBuilder.DropColumn(
                name: "cancel_reason",
                schema: "parking",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                schema: "parking",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "cancelled_by",
                schema: "parking",
                table: "parking_sessions");

            migrationBuilder.RenameColumn(
                name: "reservation_id",
                schema: "parking",
                table: "parking_sessions",
                newName: "booking_id");

            migrationBuilder.RenameColumn(
                name: "entry_time",
                schema: "parking",
                table: "parking_sessions",
                newName: "entered_at");
        }
    }
}
