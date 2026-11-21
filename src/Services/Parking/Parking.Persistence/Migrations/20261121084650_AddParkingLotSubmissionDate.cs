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
                nullable: true,
                defaultValueSql: "'0001-01-01 00:00:00+00'::timestamp with time zone");

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
