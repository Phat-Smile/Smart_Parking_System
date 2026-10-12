using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFloorsZonesAndParkingSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "floors",
                schema: "parking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_floors", x => x.id);
                    table.ForeignKey(
                        name: "fk_floors_parking_lots_lot_id",
                        column: x => x.lot_id,
                        principalSchema: "parking",
                        principalTable: "parking_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "zones",
                schema: "parking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    floor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    vehicle_group = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    current_vehicle_count = table.Column<int>(type: "integer", nullable: false),
                    has_slot_sensor = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zones", x => x.id);
                    table.CheckConstraint("ck_zones_capacity_not_negative", "capacity >= 0");
                    table.CheckConstraint("ck_zones_current_vehicle_count_not_negative", "current_vehicle_count >= 0");
                    table.ForeignKey(
                        name: "fk_zones_floors_floor_id",
                        column: x => x.floor_id,
                        principalSchema: "parking",
                        principalTable: "floors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parking_slots",
                schema: "parking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sensor_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    override_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    dispatch_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    assigned_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    effective_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parking_slots", x => x.id);
                    table.ForeignKey(
                        name: "fk_parking_slots_parking_lots_lot_id",
                        column: x => x.lot_id,
                        principalSchema: "parking",
                        principalTable: "parking_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_parking_slots_zones_zone_id",
                        column: x => x.zone_id,
                        principalSchema: "parking",
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_floors_lot_id",
                schema: "parking",
                table: "floors",
                column: "lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_parking_slots_lot_id_effective_status",
                schema: "parking",
                table: "parking_slots",
                columns: new[] { "lot_id", "effective_status" });

            migrationBuilder.CreateIndex(
                name: "ix_parking_slots_zone_id",
                schema: "parking",
                table: "parking_slots",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "ux_parking_slots_assigned_session_id",
                schema: "parking",
                table: "parking_slots",
                column: "assigned_session_id",
                unique: true,
                filter: "assigned_session_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_parking_slots_lot_slot_code",
                schema: "parking",
                table: "parking_slots",
                columns: new[] { "lot_id", "slot_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_zones_floor_id",
                schema: "parking",
                table: "zones",
                column: "floor_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parking_slots",
                schema: "parking");

            migrationBuilder.DropTable(
                name: "zones",
                schema: "parking");

            migrationBuilder.DropTable(
                name: "floors",
                schema: "parking");
        }
    }
}
