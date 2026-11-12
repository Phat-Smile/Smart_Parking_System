using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleIsDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_vehicles_account_id",
                schema: "identity",
                table: "vehicles",
                newName: "ix_vehicles_account_id");

            migrationBuilder.AddColumn<bool>(
                name: "is_default",
                schema: "identity",
                table: "vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_account_id_is_default",
                schema: "identity",
                table: "vehicles",
                columns: new[] { "account_id", "is_default" },
                unique: true,
                filter: "\"is_default\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_vehicles_account_id_is_default",
                schema: "identity",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "is_default",
                schema: "identity",
                table: "vehicles");

            migrationBuilder.RenameIndex(
                name: "ix_vehicles_account_id",
                schema: "identity",
                table: "vehicles",
                newName: "IX_vehicles_account_id");
        }
    }
}
