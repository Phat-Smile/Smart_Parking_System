using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIdentityDocumentMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "identity_back_media_id",
                schema: "identity",
                table: "profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identity_document_type",
                schema: "identity",
                table: "profiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "identity_front_media_id",
                schema: "identity",
                table: "profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "identity_issue_date",
                schema: "identity",
                table: "profiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identity_issued_by",
                schema: "identity",
                table: "profiles",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "identity_back_media_id",
                schema: "identity",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "identity_document_type",
                schema: "identity",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "identity_front_media_id",
                schema: "identity",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "identity_issue_date",
                schema: "identity",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "identity_issued_by",
                schema: "identity",
                table: "profiles");
        }
    }
}
