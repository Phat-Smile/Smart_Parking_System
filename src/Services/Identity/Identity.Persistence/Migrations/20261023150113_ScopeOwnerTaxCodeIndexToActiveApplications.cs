using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScopeOwnerTaxCodeIndexToActiveApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_accounts_owner_tax_code",
                schema: "identity",
                table: "accounts");

            migrationBuilder.CreateIndex(
                name: "ux_accounts_owner_tax_code",
                schema: "identity",
                table: "accounts",
                column: "owner_tax_code",
                unique: true,
                filter: "owner_tax_code IS NOT NULL AND owner_application_status IN ('Pending', 'Approved')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_accounts_owner_tax_code",
                schema: "identity",
                table: "accounts");

            migrationBuilder.CreateIndex(
                name: "ux_accounts_owner_tax_code",
                schema: "identity",
                table: "accounts",
                column: "owner_tax_code",
                unique: true,
                filter: "owner_tax_code IS NOT NULL");
        }
    }
}
