using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEmailVerificationSecurityAndLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "otp_code",
                schema: "identity",
                table: "email_verifications");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invalidated_at_utc",
                schema: "identity",
                table: "email_verifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "otp_hash",
                schema: "identity",
                table: "email_verifications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "row_version",
                schema: "identity",
                table: "email_verifications",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "invalidated_at_utc",
                schema: "identity",
                table: "email_verifications");

            migrationBuilder.DropColumn(
                name: "otp_hash",
                schema: "identity",
                table: "email_verifications");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "identity",
                table: "email_verifications");

            migrationBuilder.AddColumn<string>(
                name: "otp_code",
                schema: "identity",
                table: "email_verifications",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");
        }
    }
}
