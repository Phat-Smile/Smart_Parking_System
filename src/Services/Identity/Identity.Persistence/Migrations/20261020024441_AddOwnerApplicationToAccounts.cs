using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerApplicationToAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "owner_application_status",
                schema: "identity",
                table: "accounts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_rejection_reason",
                schema: "identity",
                table: "accounts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "owner_reviewed_at_utc",
                schema: "identity",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_reviewed_by",
                schema: "identity",
                table: "accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_owner_application_status",
                schema: "identity",
                table: "accounts",
                column: "owner_application_status",
                filter: "owner_application_status IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_accounts_owner_application_status",
                schema: "identity",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "owner_application_status",
                schema: "identity",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "owner_rejection_reason",
                schema: "identity",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "owner_reviewed_at_utc",
                schema: "identity",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "owner_reviewed_by",
                schema: "identity",
                table: "accounts");
        }
    }
}
