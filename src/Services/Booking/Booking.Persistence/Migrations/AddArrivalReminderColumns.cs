using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Booking.Persistence.Migrations
{
    /// <summary>
    /// Manual migration script to add arrival reminder tracking columns to the reservations table.
    /// Status: NOT RUN (pending database update when disk quota is available).
    /// </summary>
    [DbContext(typeof(BookingDbContext))]
    [Migration("20261120173000_AddArrivalReminderColumns")]
    public partial class AddArrivalReminderColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "arrival_reminder_sent_at_utc",
                schema: "booking",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "no_show_warning_sent_at_utc",
                schema: "booking",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "arrival_reminder_sent_at_utc",
                schema: "booking",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "no_show_warning_sent_at_utc",
                schema: "booking",
                table: "reservations");
        }
    }
}
