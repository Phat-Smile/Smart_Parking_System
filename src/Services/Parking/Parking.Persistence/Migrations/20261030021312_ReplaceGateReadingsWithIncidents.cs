using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceGateReadingsWithIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gate_readings",
                schema: "parking");

            migrationBuilder.CreateTable(
                name: "incidents",
                schema: "parking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    incident_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    slot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    device_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    observed_plate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    plate_confidence = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                    observed_vehicle_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolution_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_incidents", x => x.id);
                    table.ForeignKey(
                        name: "fk_incidents_parking_lots_lot_id",
                        column: x => x.lot_id,
                        principalSchema: "parking",
                        principalTable: "parking_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_incidents_parking_sessions_session_id",
                        column: x => x.session_id,
                        principalSchema: "parking",
                        principalTable: "parking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_incidents_parking_slots_slot_id",
                        column: x => x.slot_id,
                        principalSchema: "parking",
                        principalTable: "parking_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_incidents_lot_status_type_occurred_at",
                schema: "parking",
                table: "incidents",
                columns: new[] { "lot_id", "status", "incident_type", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_incidents_session_id",
                schema: "parking",
                table: "incidents",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_incidents_slot_id",
                schema: "parking",
                table: "incidents",
                column: "slot_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incidents",
                schema: "parking");

            migrationBuilder.CreateTable(
                name: "gate_readings",
                schema: "parking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    confidence = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plate_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    raw_plate = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gate_readings", x => x.id);
                    table.ForeignKey(
                        name: "fk_gate_readings_parking_lots_lot_id",
                        column: x => x.lot_id,
                        principalSchema: "parking",
                        principalTable: "parking_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gate_readings_lot_decision_received_at",
                schema: "parking",
                table: "gate_readings",
                columns: new[] { "lot_id", "decision", "received_at" });
        }
    }
}
