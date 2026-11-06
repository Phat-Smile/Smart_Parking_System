using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowBicyclesWithoutPlate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "license_plate",
                schema: "identity",
                table: "vehicles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "license_plate",
                schema: "identity",
                table: "vehicles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}
