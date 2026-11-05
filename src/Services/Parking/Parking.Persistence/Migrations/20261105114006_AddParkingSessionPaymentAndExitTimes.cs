using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParkingSessionPaymentAndExitTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "exit_time",
                schema: "parking",
                table: "parking_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_paid_at",
                schema: "parking",
                table: "parking_sessions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "exit_time",
                schema: "parking",
                table: "parking_sessions");

            migrationBuilder.DropColumn(
                name: "last_paid_at",
                schema: "parking",
                table: "parking_sessions");
        }
    }
}
