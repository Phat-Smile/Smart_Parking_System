using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "purpose",
                schema: "identity",
                table: "email_verifications",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "EmailVerification"); // every existing row is an email verification code
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "purpose",
                schema: "identity",
                table: "email_verifications");
        }
    }
}
