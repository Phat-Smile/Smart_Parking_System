using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlotParkConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "manually_parked_at",
                schema: "parking",
                table: "parking_slots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "needs_verification_since",
                schema: "parking",
                table: "parking_slots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "owner_notified_at",
                schema: "parking",
                table: "parking_slots",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "manually_parked_at",
                schema: "parking",
                table: "parking_slots");

            migrationBuilder.DropColumn(
                name: "needs_verification_since",
                schema: "parking",
                table: "parking_slots");

            migrationBuilder.DropColumn(
                name: "owner_notified_at",
                schema: "parking",
                table: "parking_slots");
        }
    }
}
