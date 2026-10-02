using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxDispatchLeasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "lock_id",
                schema: "parking",
                table: "outbox_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_until_utc",
                schema: "parking",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at_utc",
                schema: "parking",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "lock_id",
                schema: "parking",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "locked_until_utc",
                schema: "parking",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "next_attempt_at_utc",
                schema: "parking",
                table: "outbox_messages");
        }
    }
}
