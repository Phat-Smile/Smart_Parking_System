using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParkingLotSubmissionDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                schema: "parking",
                table: "parking_lots",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.Sql(
                """
                UPDATE parking.parking_lots AS lot
                SET created_at = COALESCE(
                    (
                        SELECT MIN(audit.occurred_at)
                        FROM parking.audit_logs AS audit
                        WHERE audit.lot_id = lot.id
                           OR (audit.lot_id IS NULL AND audit.target_type = 'ParkingLot' AND audit.target_id = lot.id::text)
                    ),
                    NOW()
                );
                """);

            migrationBuilder.CreateIndex(
                name: "ix_parking_lots_created_at",
                schema: "parking",
                table: "parking_lots",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_action_occurred_at",
                schema: "parking",
                table: "audit_logs",
                columns: new[] { "action", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_parking_lots_created_at",
                schema: "parking",
                table: "parking_lots");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_action_occurred_at",
                schema: "parking",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "created_at",
                schema: "parking",
                table: "parking_lots");
        }
    }
}
