using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParkingSessionExitArrivalAndExtension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "exit_arrived_at",
                schema: "parking",
                table: "parking_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "exit_window_extended_at",
                schema: "parking",
                table: "parking_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exit_window_extension_reason",
                schema: "parking",
                table: "parking_sessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_exit_window_extension",
                schema: "parking",
                table: "parking_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "exit_arrived_at",
                schema: "parking",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "exit_window_extended_at",
                schema: "parking",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "exit_window_extension_reason",
                schema: "parking",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "has_exit_window_extension",
                schema: "parking",
                table: "parking_sessions");
        }
    }
}
